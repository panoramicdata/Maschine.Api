using MeltySynth;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Text;

namespace Maschine.Demo;

internal sealed class DrumSoundfontPlayer : IDisposable
{
	internal enum InstrumentMode
	{
		PadMode = 0,
		Keyboard = 1,
		Chords = 2,
	}

	private static readonly SoundFontPreset[] s_soundFontPresets =
	[
		new(
			"pad-brd",
			InstrumentMode.PadMode,
			0,
			"Pad BRD Kit",
			"Processed_BRD_Kit.sf2",
			"https://musical-artifacts.com/artifacts/7365/Processed_BRD_Kit.sf2",
			"Processed BRD Kit (public domain) via Musical Artifacts",
			MidiChannel: 9,
			BaseNote: 36),
		new(
			"pad-zappa",
			InstrumentMode.PadMode,
			1,
			"Pad Zappa Kit",
			"ZappaKit.sf2",
			"https://archive.org/download/ZappaKit.sf2/ZappaKit.sf2",
			"ZappaKit via Internet Archive",
			MidiChannel: 9,
			BaseNote: 36),
		new(
			"pad-retro",
			InstrumentMode.PadMode,
			2,
			"Pad Retro",
			"Retro_Synth_PC.sf2",
			"https://archive.org/download/xmplayer.-7z/Retro_Synth_PC.sf2",
			"Retro Synth PC via Internet Archive",
			MidiChannel: 9,
			BaseNote: 36),
		new(
			"keys-space",
			InstrumentMode.Keyboard,
			1,
			"Space Keys",
			"LX-Space.sf2",
			"https://archive.org/download/LXSpace/LX-Space.sf2",
			"LX-Space via Internet Archive",
			MidiChannel: 0,
			BaseNote: 60),
		new(
			"keys-piano",
			InstrumentMode.Keyboard,
			0,
			"Stein Piano",
			"WST25FStein_00Sep22.sf2",
			"https://archive.org/download/WST25FStein_00Sep22.sf2/WST25FStein_00Sep22.sf2",
			"WST25FStein via Internet Archive",
			MidiChannel: 0,
			BaseNote: 62),
		new(
			"keys-retro",
			InstrumentMode.Keyboard,
			2,
			"JV Harpsichord",
			"Roland JV-1080 GM.sf2",
			"https://archive.org/download/gabedudleyssf2collection/Roland%20JV-1080%20GM.sf2",
			"Roland JV-1080 GM via Internet Archive",
			MidiChannel: 0,
			BaseNote: 64,
			ProgramNumber: 6),
		new(
			"chords-space",
			InstrumentMode.Chords,
			0,
			"Space Chords",
			"LX-Space.sf2",
			"https://archive.org/download/LXSpace/LX-Space.sf2",
			"LX-Space via Internet Archive",
			MidiChannel: 0,
			BaseNote: 48),
		new(
			"chords-zappa",
			InstrumentMode.Chords,
			1,
			"Zappa Chords",
			"ZappaKit.sf2",
			"https://archive.org/download/ZappaKit.sf2/ZappaKit.sf2",
			"ZappaKit via Internet Archive",
			MidiChannel: 0,
			BaseNote: 52),
		new(
			"chords-retro",
			InstrumentMode.Chords,
			2,
			"Retro Synth",
			"Retro_Synth_PC.sf2",
			"https://archive.org/download/xmplayer.-7z/Retro_Synth_PC.sf2",
			"Retro Synth PC via Internet Archive",
			MidiChannel: 0,
			BaseNote: 55),
	];

	private const int SampleRate = 44100;
	private const int PadPressThreshold = 220;
	private const int MaxPadPressure = 4095;
	private const int MinAudibleVelocity = 36;

	private const int PadCount = 16;

	private readonly ILogger _logger;
	private readonly IWavePlayer _output;
	private readonly DrumSynthWaveProvider _provider;
	private readonly IReadOnlyList<ResolvedSoundFontPreset> _resolvedPresets;
	private readonly Dictionary<InstrumentMode, IReadOnlyList<ResolvedSoundFontPreset>> _presetsByMode;
	private readonly int[] _selectedVariantByMode = [0, 0, 0];
	private ResolvedSoundFontPreset _activePreset;
	private InstrumentMode _activeMode;
	private bool _disposed;

	private DrumSoundfontPlayer(
		ILogger logger,
		IWavePlayer output,
		DrumSynthWaveProvider provider,
		IReadOnlyList<ResolvedSoundFontPreset> resolvedPresets,
		ResolvedSoundFontPreset activePreset,
		InstrumentMode activeMode,
		Dictionary<InstrumentMode, IReadOnlyList<ResolvedSoundFontPreset>> presetsByMode)
	{
		_logger = logger;
		_output = output;
		_provider = provider;
		_resolvedPresets = resolvedPresets;
		_activePreset = activePreset;
		_activeMode = activeMode;
		_presetsByMode = presetsByMode;
		_output.PlaybackStopped += OnPlaybackStopped;
	}

	internal static async Task<DrumSoundfontPlayer?> CreateAsync(ILogger logger, CancellationToken cancellationToken)
	{
		try
		{
			logger.LogInformation("Preparing instrument soundfonts (download if missing): {Count}", s_soundFontPresets.Length);
			var resolvedPresets = await EnsureSoundFontsAsync(logger, cancellationToken).ConfigureAwait(false);
			if (resolvedPresets.Count == 0)
			{
				logger.LogWarning("No soundfonts available. Demo drum kit disabled.");
				return null;
			}

			var presetsByMode = BuildPresetsByMode(resolvedPresets);
			if (!presetsByMode.TryGetValue(InstrumentMode.PadMode, out var padPresets) || padPresets.Count == 0)
			{
				logger.LogWarning("Pad mode presets are unavailable. Demo drum kit disabled.");
				return null;
			}

			var activePreset = padPresets[0];
			var synthesizer = new Synthesizer(activePreset.LocalPath, SampleRate);
			var provider = new DrumSynthWaveProvider(logger, activePreset, synthesizer, SampleRate);
			var output = CreateDefaultOutput(logger);

			output.Init(provider);
			output.Play();

			foreach (var (preset, index) in resolvedPresets.Select((value, index) => (value, index)))
			{
				logger.LogInformation(
					"Instrument state {State}: {Id}/{Name} mode={Mode} variant={Variant} channel={Channel} baseNote={BaseNote} program={Program} source={Path}",
					index,
					preset.Preset.Id,
					preset.Preset.DisplayName,
					preset.Preset.Mode,
					preset.Preset.Variant,
					preset.Preset.MidiChannel,
					preset.Preset.BaseNote,
					preset.Preset.ProgramNumber,
					preset.LocalPath);
			}

			if (resolvedPresets.Count < s_soundFontPresets.Length)
			{
				var unavailable = s_soundFontPresets
					.Where(p => resolvedPresets.All(r => !string.Equals(r.Preset.Id, p.Id, StringComparison.Ordinal)))
					.Select(p => p.DisplayName)
					.ToArray();
				logger.LogWarning("Unavailable instrument states: {Unavailable}", string.Join(", ", unavailable));
			}

			logger.LogInformation("Demo instrument ready: {Name} ({Attribution})", activePreset.Preset.DisplayName, activePreset.Preset.Attribution);
			return new DrumSoundfontPlayer(logger, output, provider, resolvedPresets, activePreset, InstrumentMode.PadMode, presetsByMode);
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
		var volume = normalized;
		_output.Volume = volume;
		_provider.SetMasterVolume(volume);
	}

	internal bool TryActivateMode(InstrumentMode mode, bool cycleVariant, out string instrumentName, out int variantIndex, out int variantCount)
	{
		instrumentName = "n/a";
		variantIndex = 0;
		variantCount = 0;
		if (_disposed)
		{
			return false;
		}

		if (!_presetsByMode.TryGetValue(mode, out var presets) || presets.Count == 0)
		{
			instrumentName = $"Unavailable {mode} mode";
			return false;
		}

		var modeIndex = (int)mode;
		if (_activeMode != mode)
		{
			_activeMode = mode;
		}
		else if (cycleVariant && presets.Count > 1)
		{
			_selectedVariantByMode[modeIndex] = (_selectedVariantByMode[modeIndex] + 1) % presets.Count;
		}

		if (_selectedVariantByMode[modeIndex] >= presets.Count)
		{
			_selectedVariantByMode[modeIndex] = 0;
		}

		var preset = presets[_selectedVariantByMode[modeIndex]];
		instrumentName = preset.Preset.DisplayName;
		variantIndex = _selectedVariantByMode[modeIndex];
		variantCount = presets.Count;

		if (Equals(preset, _activePreset))
		{
			return true;
		}

		_provider.SwitchPreset(preset);
		_activePreset = preset;
		_logger.LogInformation(
			"Active instrument switched -> mode={Mode} variant={Variant}/{VariantCount}: {Id}/{Name}, channel={Channel}, baseNote={BaseNote}, program={Program}",
			mode,
			variantIndex + 1,
			variantCount,
			preset.Preset.Id,
			preset.Preset.DisplayName,
			preset.Preset.MidiChannel,
			preset.Preset.BaseNote,
			preset.Preset.ProgramNumber);
		return true;
	}

	internal void PlayPad(int mappedPadIndex, int pressure)
	{
		if (_disposed || mappedPadIndex < 0 || mappedPadIndex >= PadCount)
		{
			return;
		}

		var normalizedPressure = Math.Clamp(pressure, PadPressThreshold, MaxPadPressure);
		var scaled = (normalizedPressure - PadPressThreshold) / (double)(MaxPadPressure - PadPressThreshold);
		var velocity = Math.Clamp((int)Math.Round(MinAudibleVelocity + (scaled * (127 - MinAudibleVelocity))), MinAudibleVelocity, 127);
		var note = _provider.GetNoteForPad(mappedPadIndex);
		var info = _provider.GetActivePresetInfo();
		var noteName = ToNoteName(note);
		var frequencyHz = ToFrequency(note);

		_logger.LogInformation(
			"Play pad={Pad} instrument={Id}/{Name} ch={Channel} note={MidiNote} ({NoteName}, {Frequency:0.00} Hz) pressure={Pressure} velocity={Velocity}",
			mappedPadIndex,
			info.Id,
			info.Name,
			info.Channel,
			note,
			noteName,
			frequencyHz,
			pressure,
			velocity);
		_provider.Trigger(note, velocity);
	}

	private static string ToNoteName(int midiNote)
	{
		string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
		var index = ((midiNote % 12) + 12) % 12;
		var octave = (midiNote / 12) - 1;
		return $"{names[index]}{octave}";
	}

	private static double ToFrequency(int midiNote)
	{
		return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
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

	private static async Task<IReadOnlyList<ResolvedSoundFontPreset>> EnsureSoundFontsAsync(ILogger logger, CancellationToken cancellationToken)
	{
		var cacheDirectory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Maschine.Api",
			"DemoAssets");

		Directory.CreateDirectory(cacheDirectory);
		var resolved = new List<ResolvedSoundFontPreset>(s_soundFontPresets.Length);
		var total = s_soundFontPresets.Length;

		for (var i = 0; i < s_soundFontPresets.Length; i++)
		{
			var preset = s_soundFontPresets[i];
			var localPath = Path.Combine(cacheDirectory, preset.FileName);

			if (File.Exists(localPath))
			{
				logger.LogInformation("[{Index}/{Total}] {Name,-10} {Bar} 100% (cached)", i + 1, total, preset.DisplayName, BuildProgressBar(1.0));
				var cached = new ResolvedSoundFontPreset(preset, localPath);
				resolved.Add(cached);
				continue;
			}

			try
			{
				await DownloadWithProgressAsync(preset, localPath, i + 1, total, logger, cancellationToken).ConfigureAwait(false);
				var downloaded = new ResolvedSoundFontPreset(preset, localPath);
				resolved.Add(downloaded);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				if (i == 0)
				{
					throw;
				}

				logger.LogWarning(ex, "Failed to download optional soundfont {Name}. One instrument variant will be unavailable.", preset.DisplayName);
			}
		}

		return resolved;
	}

	private static async Task DownloadWithProgressAsync(
		SoundFontPreset preset,
		string soundFontPath,
		int index,
		int total,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		logger.LogInformation("[{Index}/{Total}] {Name,-10} {Bar}   0% (downloading)", index, total, preset.DisplayName, BuildProgressBar(0.0));

		var tempPath = soundFontPath + ".download";
		if (File.Exists(tempPath))
		{
			File.Delete(tempPath);
		}

		using var httpClient = new HttpClient();
		using var response = await httpClient.GetAsync(preset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		var contentLength = response.Content.Headers.ContentLength;
		await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
		await using (var destination = File.Create(tempPath))
		{
			var buffer = new byte[64 * 1024];
			long totalRead = 0;
			var lastPct = -1;

			while (true)
			{
				var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
				if (read == 0)
				{
					break;
				}

				await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
				totalRead += read;

				if (contentLength is null || contentLength <= 0)
				{
					continue;
				}

				var progress = Math.Clamp((double)totalRead / contentLength.Value, 0.0, 1.0);
				var pct = (int)Math.Round(progress * 100.0);
				if (pct == lastPct || pct % 5 != 0)
				{
					continue;
				}

				lastPct = pct;
				logger.LogInformation("[{Index}/{Total}] {Name,-10} {Bar} {Percent,3}%", index, total, preset.DisplayName, BuildProgressBar(progress), pct);
			}
		}

		if (File.Exists(soundFontPath))
		{
			File.Delete(soundFontPath);
		}

		File.Move(tempPath, soundFontPath);
		logger.LogInformation("[{Index}/{Total}] {Name,-10} {Bar} 100%", index, total, preset.DisplayName, BuildProgressBar(1.0));
	}

	private static string BuildProgressBar(double progress)
	{
		const int width = 24;
		var clamped = Math.Clamp(progress, 0.0, 1.0);
		var filled = (int)Math.Round(clamped * width);
		var builder = new StringBuilder(width + 2);
		builder.Append('[');
		builder.Append('#', filled);
		builder.Append('.', width - filled);
		builder.Append(']');
		return builder.ToString();
	}

	private static Dictionary<InstrumentMode, IReadOnlyList<ResolvedSoundFontPreset>> BuildPresetsByMode(IReadOnlyList<ResolvedSoundFontPreset> presets)
	{
		return presets
			.GroupBy(p => p.Preset.Mode)
			.ToDictionary(
				g => g.Key,
				g => (IReadOnlyList<ResolvedSoundFontPreset>)g.OrderBy(p => p.Preset.Variant).ToArray());
	}

	private sealed record SoundFontPreset(
		string Id,
		InstrumentMode Mode,
		int Variant,
		string DisplayName,
		string FileName,
		string Url,
		string Attribution,
		int MidiChannel,
		int BaseNote,
		int ProgramNumber = -1);

	private sealed record ResolvedSoundFontPreset(SoundFontPreset Preset, string LocalPath);

	private sealed class DrumSynthWaveProvider : IWaveProvider
	{
		private const int BytesPerSample = sizeof(short);
		private const int ChannelCount = 2;
		private const int BytesPerFrame = BytesPerSample * ChannelCount;

		private readonly ILogger _logger;
		private readonly int _sampleRate;
		private readonly object _gate = new();
		private ResolvedSoundFontPreset _activePreset;
		private Synthesizer _synthesizer;
		private float[] _left = [];
		private float[] _right = [];
		private float _masterVolume = 0.5F;
		private bool _readFailureLogged;

		internal DrumSynthWaveProvider(ILogger logger, ResolvedSoundFontPreset activePreset, Synthesizer synthesizer, int sampleRate)
		{
			_logger = logger;
			_activePreset = activePreset;
			_sampleRate = sampleRate;
			_synthesizer = synthesizer;
			ApplyPresetProgram();
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

		internal void SwitchPreset(ResolvedSoundFontPreset preset)
		{
			lock (_gate)
			{
				_synthesizer.NoteOffAll(true);
				_activePreset = preset;
				_synthesizer = new Synthesizer(preset.LocalPath, _sampleRate);
				ApplyPresetProgram();
			}
		}

		internal int GetNoteForPad(int padIndex)
		{
			lock (_gate)
			{
				return _activePreset.Preset.BaseNote + padIndex;
			}
		}

		internal (string Id, string Name, int Channel, int BaseNote) GetActivePresetInfo()
		{
			lock (_gate)
			{
				return (
					_activePreset.Preset.Id,
					_activePreset.Preset.DisplayName,
					_activePreset.Preset.MidiChannel,
					_activePreset.Preset.BaseNote);
			}
		}

		internal void Trigger(int midiNote, int velocity)
		{
			try
			{
				lock (_gate)
				{
					var channel = _activePreset.Preset.MidiChannel;
					_synthesizer.NoteOff(channel, midiNote);
					_synthesizer.NoteOn(channel, midiNote, velocity);
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
					_synthesizer = new Synthesizer(_activePreset.LocalPath, _sampleRate);
					ApplyPresetProgram();
				}

				_logger.LogWarning(lastError, "Rebuilt drum synthesizer instance after render failure.");
			}
			catch (Exception recoveryEx)
			{
				_logger.LogError(recoveryEx, "Failed to rebuild drum synthesizer after render failure.");
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

		private void ApplyPresetProgram()
		{
			var preset = _activePreset.Preset;
			if (preset.ProgramNumber < 0)
			{
				return;
			}

			_synthesizer.ProcessMidiMessage(preset.MidiChannel, 0xC0, preset.ProgramNumber, 0);
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