using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Maschine.Api.Widgets;
using Microsoft.Extensions.Logging;

namespace Maschine.Demo;

/// <summary>
/// Button and knob handling for <see cref="DemoController"/>: key events, instrument-mode
/// selection and dashboard switching. Pads and the touch strip live in sibling files.
/// </summary>
internal sealed partial class DemoController
{
	private static readonly Dictionary<int, DrumSoundfontPlayer.InstrumentMode> s_instrumentModeByButton = new()
	{
		[(int)MikroMk3Button.PadMode] = DrumSoundfontPlayer.InstrumentMode.PadMode,
		[(int)MikroMk3Button.Keyboard] = DrumSoundfontPlayer.InstrumentMode.Keyboard,
		[(int)MikroMk3Button.Chords] = DrumSoundfontPlayer.InstrumentMode.Chords,
	};

	// The instrument mode the mode buttons select between.
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
			_logger.LogInformation("Active Dashboard -> {DashboardIndex}: {DashboardTitle}", dashboardIndex, DemoDashboards.GetTitle(dashboardIndex));
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
		=> s_instrumentModeByButton.TryGetValue(buttonIndex, out mode);
}
