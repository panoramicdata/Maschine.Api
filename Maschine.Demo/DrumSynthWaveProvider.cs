using MeltySynth;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace Maschine.Demo;

/// <summary>
/// Renders the active soundfont's MIDI notes into audio samples for NAudio.
/// </summary>
internal sealed class DrumSynthWaveProvider : IWaveProvider
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
