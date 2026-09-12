using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Maschine.Api.Widgets;
using Microsoft.Extensions.Logging;

namespace Maschine.Demo;

/// <summary>
/// Input handling for <see cref="DemoController"/>: the pad, button, encoder and touch-strip
/// event handlers, and the debounce and coalescing they rely on.
/// </summary>
internal sealed partial class DemoController
{
	// Owned by the input half: the touch-strip level last pushed to the device, and the
	// instrument mode the mode buttons select between.
	private int _touchStripRenderedLevel = -1;
	private DrumSoundfontPlayer.InstrumentMode _activeInstrumentMode = DrumSoundfontPlayer.InstrumentMode.PadMode;

	private void OnEncoderTouchChanged(EncoderTouchState state)
	{
		var dashboardIndex = state.KnobValue % _dashboards.Length;
		var changed = false;
		lock (_animationSync)
		{
			if (_selectedDashboardIndex != dashboardIndex)
			{
				_selectedDashboardIndex = dashboardIndex;
				changed = true;
			}
		}

		var touchStr = state.IsTouched ? "touched" : "released";
		_logger.LogInformation("Knob {TouchState}, position={KnobValue}, Dashboard={DashboardIndex}", touchStr, state.KnobValue, dashboardIndex);
		if (changed)
		{
			_logger.LogInformation("Active Dashboard -> {DashboardIndex}: {DashboardTitle}", dashboardIndex, GetDashboardTitle(dashboardIndex));
		}
	}

	private void OnKeyEvent(KeyEvent evt)
	{
		if (_buttons is null)
		{
			return;
		}

		if (evt.Type == KeyEventType.KeyDown || evt.Type == KeyEventType.KeyUp)
		{
			_logger.LogInformation("Key edge: {Button} -> {Type}", evt.Button, evt.Type);
			return;
		}

		if (evt.Type == KeyEventType.KeyPressed)
		{
			OnKeyPressed(evt);
			return;
		}

		if (evt.Type == KeyEventType.KeyOn || evt.Type == KeyEventType.KeyOff)
		{
			_logger.LogInformation("Key state: {Button} -> {Type}", evt.Button, evt.Type);
		}
	}

	private void OnKeyPressed(KeyEvent evt)
	{
		if (evt.Button == MikroMk3Button.MachineLogo)
		{
			ToggleDashboardInvert(evt.Button);
			return;
		}

		if (TryGetInstrumentModeForButton((int)evt.Button, out var requestedMode))
		{
			_ = HandleInstrumentModeButtonAsync(requestedMode, evt.Button.ToString());
		}
	}

	private void ToggleDashboardInvert(MikroMk3Button button)
	{
		bool inverted;
		lock (_animationSync)
		{
			_isDashboardInverted = !_isDashboardInverted;
			inverted = _isDashboardInverted;
		}

		_logger.LogInformation("Key action: {Button} -> Dashboard invert {InvertState}", button, inverted ? "ON" : "OFF");
	}

	private async Task HandleInstrumentModeButtonAsync(DrumSoundfontPlayer.InstrumentMode requestedMode, string buttonDescriptor)
	{
		var cycleVariant = requestedMode == _activeInstrumentMode;
		if (_drumPlayer is null)
		{
			_logger.LogWarning("Instrument mode change ignored: drum player unavailable.");
			return;
		}

		if (_drumPlayer.TryActivateMode(requestedMode, cycleVariant, out var instrumentName, out var variantIndex, out var variantCount))
		{
			_activeInstrumentMode = requestedMode;
			_logger.LogInformation(
				"Instrument mode action: {ButtonDescriptor} -> mode={Mode}, variant={Variant}/{VariantCount}, instrument={Instrument}",
				buttonDescriptor,
				requestedMode,
				variantIndex + 1,
				variantCount,
				instrumentName);
		}
		else
		{
			_logger.LogWarning("Instrument mode switch failed: mode={Mode}, details={Details}", requestedMode, instrumentName);
		}

	}

	private static bool TryGetInstrumentModeForButton(int buttonIndex, out DrumSoundfontPlayer.InstrumentMode mode)
	{
		mode = DrumSoundfontPlayer.InstrumentMode.PadMode;
		if (buttonIndex == (int)MikroMk3Button.PadMode)
		{
			mode = DrumSoundfontPlayer.InstrumentMode.PadMode;
			return true;
		}

		if (buttonIndex == (int)MikroMk3Button.Keyboard)
		{
			mode = DrumSoundfontPlayer.InstrumentMode.Keyboard;
			return true;
		}

		if (buttonIndex == (int)MikroMk3Button.Chords)
		{
			mode = DrumSoundfontPlayer.InstrumentMode.Chords;
			return true;
		}

		return false;
	}

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

	private void OnEncoderChanged(EncoderDelta delta)
	{
		const int TouchStripNoiseFloor = 20;
		const int EncoderNoiseFloor = 24;
		const int LogThrottleMs = 60;

		var noiseFloor = delta.Index == TouchStripEncoderIndex ? TouchStripNoiseFloor : EncoderNoiseFloor;
		if (Math.Abs(delta.Delta) < noiseFloor)
		{
			return;
		}

		var step = Math.Sign(delta.Delta);
		if (step == 0)
		{
			return;
		}

		if (delta.Index == TouchStripEncoderIndex)
		{
			// Calibrated axis for strip LEDs.
			var nowUtc = DateTime.UtcNow;
			bool shouldLog;
			int level;
			lock (_animationSync)
			{
				shouldLog = (nowUtc - _lastEncoderLogUtc[delta.Index]).TotalMilliseconds >= LogThrottleMs;
				if (shouldLog)
				{
					_lastEncoderLogUtc[delta.Index] = nowUtc;
				}

				_touchStripLevel = Math.Clamp(_touchStripLevel + step, 0, MaschineDeviceConstants.MikroMk3TouchStripLedCount);
				level = _touchStripLevel;
			}

			_drumPlayer?.SetVolumeFromStripLevel(level);

			if (shouldLog)
			{
				_logger.LogInformation("Slider at position {Position} (drum volume {VolumePercent}%)", level, (int)Math.Round((level / 25.0) * 100.0));
				_logger.LogDebug("Slider delta {Delta:+#;-#;0} (encoder index {Index})", delta.Delta, delta.Index);
			}

			_ = UpdateTouchStripLedsCoalescedAsync();
		}
		else
		{
			// Ignore auxiliary encoder noise in the demo; it obscures pad/button logs.
		}
	}

	private async Task UpdateTouchStripLedsCoalescedAsync()
	{
		if (_touchStrip is null)
		{
			return;
		}

		if (!await _touchStripUpdateGate.WaitAsync(0).ConfigureAwait(false))
		{
			return;
		}

		try
		{
			// Re-check after each write: the level can move again while the HID write is in flight,
			// and this loop is the only writer, so it must drain to the latest value before exiting.
			while (ReadTouchStripLevel() != _touchStripRenderedLevel)
			{
				var level = ReadTouchStripLevel();
				await _touchStrip.SetLedsAsync(BuildTouchStripLeds(level)).ConfigureAwait(false);
				_touchStripRenderedLevel = level;
			}
		}
		finally
		{
			_touchStripUpdateGate.Release();
		}
	}

	private int ReadTouchStripLevel()
	{
		lock (_animationSync)
		{
			return _touchStripLevel;
		}
	}

	private static PadColor[] BuildTouchStripLeds(int level)
	{
		var leds = new PadColor[MaschineDeviceConstants.MikroMk3TouchStripLedCount];
		var activeColor = level == 0 ? PadColor.Off : s_touchStripDemoColors[level - 1];
		for (var i = 0; i < leds.Length; i++)
		{
			leds[i] = i < level ? activeColor : PadColor.Off;
		}

		return leds;
	}
}
