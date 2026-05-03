using MeltySynth;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Maschine.Demo;

internal sealed class DrumSoundfontPlayer : IDisposable
{
	private const string SoundFontFileName = "Processed_BRD_Kit.sf2";
	private const string SoundFontUrl = "https://musical-artifacts.com/artifacts/7365/Processed_BRD_Kit.sf2";
	private const string SoundFontAttribution = "Processed BRD Kit (public domain) via Musical Artifacts";
	private const int SampleRate = 44100;
	private const int PadPressThreshold = 220;
	private const int MaxPadPressure = 4095;
	private const int MinAudibleVelocity = 36;

	private static readonly int[] s_padNotes =
	[
		36, 38, 42, 46,
		41, 45, 47, 49,
		51, 57, 40, 37,
		48, 50, 35, 36,
	];

	private readonly ILogger _logger;
	private readonly IWavePlayer _output;
	private readonly DrumSynthWaveProvider _provider;
	private bool _disposed;

	private DrumSoundfontPlayer(ILogger logger, IWavePlayer output, DrumSynthWaveProvider provider)
	{
		_logger = logger;
		_output = output;
		_provider = provider;
		_output.PlaybackStopped += OnPlaybackStopped;
	}

	internal static async Task<DrumSoundfontPlayer?> CreateAsync(ILogger logger, CancellationToken cancellationToken)
	{
		try
		{
			var soundFontPath = await EnsureSoundFontAsync(logger, cancellationToken).ConfigureAwait(false);
			var synthesizer = new Synthesizer(soundFontPath, SampleRate);
			var provider = new DrumSynthWaveProvider(logger, soundFontPath, synthesizer, SampleRate);
			var output = CreateDefaultOutput(logger);

			output.Init(provider);
			output.Play();

			logger.LogInformation("Demo drum kit ready: {Attribution}", SoundFontAttribution);
			return new DrumSoundfontPlayer(logger, output, provider);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Demo drum kit unavailable. Continuing without audio playback.");
			return null;
		}
	}

	internal void SetVolumeFromStripLevel(int level)
	{
		var normalized = Math.Clamp(level / 25F, 0F, 1F);
		var volume = 0.35F + (normalized * 0.65F);
		_provider.SetMasterVolume(volume);
	}

	internal void PlayPad(int mappedPadIndex, int pressure)
	{
		if (_disposed || mappedPadIndex < 0 || mappedPadIndex >= s_padNotes.Length)
		{
			return;
		}

		var normalizedPressure = Math.Clamp(pressure, PadPressThreshold, MaxPadPressure);
		var scaled = (normalizedPressure - PadPressThreshold) / (double)(MaxPadPressure - PadPressThreshold);
		var velocity = Math.Clamp((int)Math.Round(MinAudibleVelocity + (scaled * (127 - MinAudibleVelocity))), MinAudibleVelocity, 127);
		var note = s_padNotes[mappedPadIndex];

		_logger.LogDebug("Drum trigger: pad={Pad} note={Note} pressure={Pressure} velocity={Velocity}", mappedPadIndex, note, pressure, velocity);
		_provider.Trigger(note, velocity);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_output.PlaybackStopped -= OnPlaybackStopped;

		try
		{
			_output.Stop();
		}
		catch (Exception ex)
		{
			_logger.LogDebug(ex, "Ignoring drum output stop failure during disposal.");
		}

		_output.Dispose();
	}

	private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
	{
		if (_disposed)
		{
			_logger.LogDebug("Demo drum playback stopped during disposal.");
			return;
		}

		if (e.Exception is not null)
		{
			_logger.LogError(e.Exception, "Demo drum playback engine stopped due to an exception.");
			return;
		}

		_logger.LogWarning("Demo drum playback engine stopped unexpectedly without an exception.");
	}

	private static WasapiOut CreateDefaultOutput(ILogger logger)
	{
		using var enumerator = new MMDeviceEnumerator();
		var defaultRender = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
		logger.LogInformation("Windows default audio output: {Name}", defaultRender.FriendlyName);

		return new WasapiOut(defaultRender, AudioClientShareMode.Shared, false, 80);
	}

	private static async Task<string> EnsureSoundFontAsync(ILogger logger, CancellationToken cancellationToken)
	{
		var cacheDirectory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Maschine.Api",
			"DemoAssets");

		Directory.CreateDirectory(cacheDirectory);

		var soundFontPath = Path.Combine(cacheDirectory, SoundFontFileName);
		if (File.Exists(soundFontPath))
		{
			logger.LogInformation("Using cached demo drum kit at {Path}", soundFontPath);
			return soundFontPath;
		}

		logger.LogInformation("Downloading demo drum kit from {Url}", SoundFontUrl);

		var tempPath = soundFontPath + ".download";
		if (File.Exists(tempPath))
		{
			File.Delete(tempPath);
		}

		using var httpClient = new HttpClient();
		using var response = await httpClient.GetAsync(SoundFontUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
		await using (var destination = File.Create(tempPath))
		{
			await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
		}

		if (File.Exists(soundFontPath))
		{
			File.Delete(soundFontPath);
		}

		File.Move(tempPath, soundFontPath);
		return soundFontPath;
	}

	private sealed class DrumSynthWaveProvider : IWaveProvider
	{
		private const int BytesPerSample = sizeof(short);
		private const int ChannelCount = 2;
		private const int BytesPerFrame = BytesPerSample * ChannelCount;
		private const int PercussionChannel = 9;

		private readonly ILogger _logger;
		private readonly string _soundFontPath;
		private readonly int _sampleRate;
		private Synthesizer _synthesizer;
		private readonly object _gate = new();
		private readonly HashSet<int> _quarantinedNotes = [];
		private readonly Dictionary<int, int> _noteFailureCounts = [];
		private float[] _left = [];
		private float[] _right = [];
		private float _masterVolume = 0.5F;
		private bool _readFailureLogged;
		private int _lastTriggeredNote = -1;
		private int _lastTriggeredVelocity;
		private DateTime _lastTriggeredAtUtc = DateTime.MinValue;

		internal DrumSynthWaveProvider(ILogger logger, string soundFontPath, Synthesizer synthesizer, int sampleRate)
		{
			_logger = logger;
			_soundFontPath = soundFontPath;
			_sampleRate = sampleRate;
			_synthesizer = synthesizer;
			WaveFormat = new WaveFormat(sampleRate, 16, ChannelCount);
		}

		public WaveFormat WaveFormat { get; }

		internal void SetMasterVolume(float volume)
		{
			lock (_gate)
			{
				_masterVolume = Math.Clamp(volume, 0F, 1F);
			}
		}

		internal void Trigger(int midiNote, int velocity)
		{
			try
			{
				var noteToTrigger = midiNote;
				lock (_gate)
				{
					if (_quarantinedNotes.Contains(noteToTrigger))
					{
						noteToTrigger = 38;
					}

					// Prevent unbounded voice accumulation on rapid retriggers.
					// On this demo kit, aggressive overlap can increase MeltySynth instability.
					_synthesizer.NoteOff(PercussionChannel, noteToTrigger);

					_lastTriggeredNote = noteToTrigger;
					_lastTriggeredVelocity = velocity;
					_lastTriggeredAtUtc = DateTime.UtcNow;
					_synthesizer.NoteOn(PercussionChannel, noteToTrigger, velocity);
				}

				if (noteToTrigger != midiNote)
				{
					_logger.LogWarning("Drum note {OriginalNote} is quarantined due to prior synth failures. Remapped to safe note {FallbackNote}.", midiNote, noteToTrigger);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Drum synth NoteOn failed (note {Note}, velocity {Velocity}).", midiNote, velocity);
			}
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			try
			{
				var frameCount = count / BytesPerFrame;
				EnsureCapacity(frameCount);

				float volume;
				lock (_gate)
				{
					volume = _masterVolume;
					_synthesizer.Render(_left.AsSpan(0, frameCount), _right.AsSpan(0, frameCount));
				}

				var index = offset;
				for (var i = 0; i < frameCount; i++)
				{
					WriteSample(buffer, ref index, _left[i] * volume);
					WriteSample(buffer, ref index, _right[i] * volume);
				}

				if (_readFailureLogged)
				{
					_readFailureLogged = false;
					_logger.LogInformation("Drum synth render recovered after previous failure.");
				}

				return frameCount * BytesPerFrame;
			}
			catch (Exception ex)
			{
				TrackRenderFailureCandidate(ex);

				if (!_readFailureLogged)
				{
					_readFailureLogged = true;
					_logger.LogError(ex, "Drum synth render loop failed. Output will be silent until the render loop recovers.");
				}

				TryRecoverSynth(ex);

				Array.Clear(buffer, offset, count);
				return count;
			}
		}

		private void TryRecoverSynth(Exception lastError)
		{
			try
			{
				lock (_gate)
				{
					_synthesizer = new Synthesizer(_soundFontPath, _sampleRate);
				}

				_logger.LogWarning(lastError, "Rebuilt drum synthesizer instance after render failure.");
			}
			catch (Exception recoveryEx)
			{
				_logger.LogError(recoveryEx, "Failed to rebuild drum synthesizer after render failure.");
			}
		}

		private void TrackRenderFailureCandidate(Exception ex)
		{
			if (_lastTriggeredNote < 0)
			{
				return;
			}

			// Attribute the failure to the most recent trigger when it happened very recently.
			if ((DateTime.UtcNow - _lastTriggeredAtUtc).TotalMilliseconds > 500)
			{
				return;
			}

			var failures = _noteFailureCounts.TryGetValue(_lastTriggeredNote, out var existing)
				? existing + 1
				: 1;
			_noteFailureCounts[_lastTriggeredNote] = failures;

			_logger.LogWarning(ex,
				"Attributed synth render failure to recent note {Note} (velocity {Velocity}), failure count {Count}.",
				_lastTriggeredNote,
				_lastTriggeredVelocity,
				failures);

			if (failures >= 2 && _quarantinedNotes.Add(_lastTriggeredNote))
			{
				_logger.LogError("Quarantining unstable drum note {Note} after repeated render failures; future triggers will use safe fallback note 38.", _lastTriggeredNote);
			}
		}

		private void EnsureCapacity(int frameCount)
		{
			if (_left.Length >= frameCount)
			{
				return;
			}

			_left = new float[frameCount];
			_right = new float[frameCount];
		}

		private static void WriteSample(byte[] buffer, ref int index, float sample)
		{
			var clamped = Math.Clamp(sample, -1F, 1F);
			var pcm = (short)Math.Round(clamped * short.MaxValue);
			buffer[index++] = (byte)(pcm & 0xFF);
			buffer[index++] = (byte)((pcm >> 8) & 0xFF);
		}
	}
}