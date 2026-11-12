using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Latch and fire behaviours for <see cref="MaschineButtons"/>.
/// </summary>
internal sealed partial class MaschineButtons
{
	/// <summary>
	/// LatchLong turns on immediately on press; a press while already on merely arms the
	/// release, so that holding the key is what turns it back off.
	/// </summary>
	private void PressLatchLong(MikroMk3Button button, int index)
	{
		if (_keyOnStates[index])
		{
			_latchLongReleaseArmed[index] = true;
			return;
		}

		SetKeyOnState(button, true, true);
		_latchLongReleaseArmed[index] = false;
	}

	/// <summary>
	/// LatchShort is the mirror of <see cref="PressLatchLong"/>: it turns off on press when
	/// already on, and otherwise arms the release to turn it on.
	/// </summary>
	private void PressLatchShort(MikroMk3Button button, int index)
	{
		if (!_keyOnStates[index])
		{
			_latchShortReleaseArmed[index] = true;
			return;
		}

		SetKeyOnState(button, false, true);
		_latchShortReleaseArmed[index] = false;
	}

	private void ReleaseLatchLong(MikroMk3Button button, int index)
	{
		if (!_keyOnStates[index] || !_latchLongReleaseArmed[index])
		{
			return;
		}

		SetKeyOnState(button, false, false);
		_latchLongReleaseArmed[index] = false;
	}

	private void ReleaseLatchShort(MikroMk3Button button, int index)
	{
		if (_keyOnStates[index] || !_latchShortReleaseArmed[index])
		{
			return;
		}

		SetKeyOnState(button, true, false);
		_latchShortReleaseArmed[index] = false;
	}

	private void ApplyKeyToggle(MikroMk3Button button)
	{
		var index = (int)button;
		SetKeyOnState(button, !_keyOnStates[index], _states[index].IsPressed);
	}

	private void ApplyFireActivation(MikroMk3Button button)
	{
		var index = (int)button;
		if (_groupByButton[index] >= 0)
		{
			ActivateRadioGroupSelection(button);
		}

		_ = FlashFireLedAsync(button);
	}

	private async Task FlashFireLedAsync(MikroMk3Button button)
	{
		var index = (int)button;
		var duration = _fireFlashDurationOverrideMs[index] ?? _globalFireFlashDurationMs;
		if (duration <= 0)
		{
			return;
		}

		var generation = Interlocked.Increment(ref _flashGenerationByButton[index]);
		try
		{
			await SetLedInternalAsync(index, ManagedOnBrightness, CancellationToken.None).ConfigureAwait(false);
			await Task.Delay(duration).ConfigureAwait(false);
			if (Volatile.Read(ref _flashGenerationByButton[index]) != generation)
			{
				return;
			}

			var restore = _keyOnStates[index] ? ManagedOnBrightness : (byte)0;
			await SetLedInternalAsync(index, restore, CancellationToken.None).ConfigureAwait(false);
		}
		catch
		{
			// Fire LED pulse is best-effort.
		}
	}
}
