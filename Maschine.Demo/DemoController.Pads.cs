using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Microsoft.Extensions.Logging;

namespace Maschine.Demo;

/// <summary>
/// Pad handling for <see cref="DemoController"/>: pressure hysteresis, retrigger debounce and the
/// LED feedback for each pad.
/// </summary>
internal sealed partial class DemoController
{
	private void OnPadChanged(PadState state)
	{
		const int PressThreshold = 220;
		const int ReleaseThreshold = 80;

		var (wasDown, isDown, ignoreRetrigger) = UpdatePadDownState(state, PressThreshold, ReleaseThreshold);

		if (!wasDown && isDown)
		{
			OnPadPressed(state, ignoreRetrigger);
			return;
		}

		if (wasDown && !isDown)
		{
			var restoreColor = GetPadBaseColor(state.Index);
			_logger.LogInformation("Pad UP:   P{PadNumber,2} (raw {PadRaw,2}), pressure={Pressure} -> {Color}", ToUserPadNumber(state.Index), state.Index, state.Pressure, FormatColor(restoreColor));
			_ = TrySetPadColorAsync(state.Index, restoreColor);
		}
	}

	/// <summary>
	/// Applies hysteresis to one pad's pressure reading and records the resulting down state.
	/// </summary>
	/// <returns>
	/// The previous and new down states, plus whether a new press falls inside the retrigger
	/// debounce window and should therefore be ignored.
	/// </returns>
	private (bool WasDown, bool IsDown, bool IgnoreRetrigger) UpdatePadDownState(PadState state, int pressThreshold, int releaseThreshold)
	{
		lock (_animationSync)
		{
			var wasDown = _padDown[state.Index];
			if (wasDown)
			{
				var stillDown = state.Pressure > releaseThreshold;
				_padDown[state.Index] = stillDown;
				return (true, stillDown, false);
			}

			var isDown = state.Pressure >= pressThreshold;
			_padDown[state.Index] = isDown;
			if (!isDown)
			{
				return (false, false, false);
			}

			var nowUtc = DateTime.UtcNow;
			var ignoreRetrigger = (nowUtc - _lastPadDownUtc[state.Index]) <= s_padRetriggerDebounce;
			if (!ignoreRetrigger)
			{
				_lastPadDownUtc[state.Index] = nowUtc;
			}

			return (false, true, ignoreRetrigger);
		}
	}

	private void OnPadPressed(PadState state, bool ignoreRetrigger)
	{
		if (ignoreRetrigger)
		{
			_logger.LogDebug("Pad DOWN ignored by debounce: P{PadNumber,2} (raw {PadRaw,2}), pressure={Pressure}", ToUserPadNumber(state.Index), state.Index, state.Pressure);
			return;
		}

		_logger.LogInformation("Pad DOWN: P{PadNumber,2} (raw {PadRaw,2}), pressure={Pressure} -> white", ToUserPadNumber(state.Index), state.Index, state.Pressure);
		_drumPlayer?.PlayPad(MapPadIndexWithVerticalFlip(state.Index), state.Pressure);
		_ = TrySetPadColorAsync(state.Index, PadColor.White);
	}
}
