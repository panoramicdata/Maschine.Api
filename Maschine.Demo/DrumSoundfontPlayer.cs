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
			logger.LogInformation("Preparing instrument soundfonts (download if missing): {Count}", SoundFontLibrary.Catalogue.Length);
			var resolvedPresets = await SoundFontLibrary.EnsureSoundFontsAsync(logger, cancellationToken).ConfigureAwait(false);
			if (resolvedPresets.Count == 0)
			{
				logger.LogWarning("No soundfonts available. Demo drum kit disabled.");
				return null;
			}

			var presetsByMode = SoundFontLibrary.BuildPresetsByMode(resolvedPresets);
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

			LogResolvedPresets(logger, resolvedPresets, activePreset);
			return new DrumSoundfontPlayer(logger, output, provider, resolvedPresets, activePreset, InstrumentMode.PadMode, presetsByMode);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Cancellation is deliberately excluded by the filter so that shutdown propagates
			// rather than being reported as an unavailable drum kit.
			logger.LogWarning(ex, "Demo drum kit unavailable. Continuing without audio playback.");
			return null;
		}
	}

	private static void LogResolvedPresets(
		ILogger logger,
		IReadOnlyList<ResolvedSoundFontPreset> resolvedPresets,
		ResolvedSoundFontPreset activePreset)
	{
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

		if (resolvedPresets.Count < SoundFontLibrary.Catalogue.Length)
		{
			var unavailable = SoundFontLibrary.Catalogue
				.Where(p => resolvedPresets.All(r => !string.Equals(r.Preset.Id, p.Id, StringComparison.Ordinal)))
				.Select(p => p.DisplayName)
				.ToArray();
			logger.LogWarning("Unavailable instrument states: {Unavailable}", string.Join(", ", unavailable));
		}

		logger.LogInformation("Demo instrument ready: {Name} ({Attribution})", activePreset.Preset.DisplayName, activePreset.Preset.Attribution);
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

		var modeIndex = SelectVariant(mode, cycleVariant, presets.Count);
		var preset = presets[modeIndex];
		instrumentName = preset.Preset.DisplayName;
		variantIndex = modeIndex;
		variantCount = presets.Count;

		if (!Equals(preset, _activePreset))
		{
			SwitchToPreset(mode, preset, variantIndex, variantCount);
		}

		return true;
	}

	/// <summary>
	/// Makes <paramref name="mode"/> active and returns the variant index to play. Re-selecting the
	/// mode that is already active advances to the next variant when <paramref name="cycleVariant"/>
	/// is set; selecting a different mode keeps that mode's previously chosen variant.
	/// </summary>
	private int SelectVariant(InstrumentMode mode, bool cycleVariant, int presetCount)
	{
		var modeIndex = (int)mode;
		if (_activeMode != mode)
		{
			_activeMode = mode;
		}
		else if (cycleVariant && presetCount > 1)
		{
			_selectedVariantByMode[modeIndex] = (_selectedVariantByMode[modeIndex] + 1) % presetCount;
		}

		if (_selectedVariantByMode[modeIndex] >= presetCount)
		{
			_selectedVariantByMode[modeIndex] = 0;
		}

		return _selectedVariantByMode[modeIndex];
	}

	private void SwitchToPreset(InstrumentMode mode, ResolvedSoundFontPreset preset, int variantIndex, int variantCount)
	{
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

	private static WasapiPlayer CreateDefaultOutput(ILogger logger)
	{
		using var enumerator = new MMDeviceEnumerator();
		var defaultRender = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
		logger.LogInformation("Windows default audio output: {Name}", defaultRender.FriendlyName);

		// Shared mode with polling sync at 80 ms, matching what the old WasapiOut constructor
		// arguments requested before NAudio 3 replaced it with this builder.
		return new WasapiPlayerBuilder()
			.WithDevice(defaultRender)
			.WithSharedMode()
			.WithPollingSync()
			.WithLatency(80)
			.Build();
	}
}
