using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Key-mode dispatch for <see cref="MaschineButtons"/>: routes press and release events to the
/// latch, fire and radio-group behaviours kept in the sibling files.
/// </summary>
internal sealed partial class MaschineButtons
{
	private void ProcessKeyModeEvent(MikroMk3Button button, bool isPressed)
	{
		var index = (int)button;
		var mode = _keyModes[index];

		if (mode == KeyMode.EventsOnly)
		{
			EmitKeyEvent(button, isPressed ? KeyEventType.KeyDown : KeyEventType.KeyUp, isPressed, _keyOnStates[index]);
			return;
		}

		if (isPressed)
		{
			OnKeyDown(button, mode);
		}
		else
		{
			OnKeyUp(button, mode);
		}
	}

	private void OnKeyDown(MikroMk3Button button, KeyMode mode)
	{
		var index = (int)button;
		switch (mode)
		{
			case KeyMode.LatchEarly:
				ApplyKeyToggle(button);
				break;

			case KeyMode.LatchLong:
				PressLatchLong(button, index);
				break;

			case KeyMode.LatchShort:
				PressLatchShort(button, index);
				break;

			case KeyMode.OnWhenPressed:
				SetKeyOnState(button, true, true);
				break;

			case KeyMode.FireEarly:
				EmitKeyEvent(button, KeyEventType.KeyPressed, true, _keyOnStates[index]);
				ApplyFireActivation(button);
				break;

			default:
				// Remaining modes act on release only; see OnKeyUp.
				break;
		}
	}

	private void OnKeyUp(MikroMk3Button button, KeyMode mode)
	{
		var index = (int)button;
		switch (mode)
		{
			case KeyMode.LatchLate:
				ApplyKeyToggle(button);
				break;

			case KeyMode.LatchLong:
				ReleaseLatchLong(button, index);
				break;

			case KeyMode.LatchShort:
				ReleaseLatchShort(button, index);
				break;

			case KeyMode.OnWhenPressed:
				SetKeyOnState(button, false, false);
				break;

			case KeyMode.FireLate:
				EmitKeyEvent(button, KeyEventType.KeyPressed, false, _keyOnStates[index]);
				ApplyFireActivation(button);
				break;

			default:
				// Remaining modes act on press only; see OnKeyDown.
				break;
		}
	}

	private void SetKeyOnState(MikroMk3Button button, bool isOn, bool isPressed)
	{
		var index = (int)button;
		if (!TryUpdateRadioGroup(index, isOn))
		{
			return;
		}

		if (_keyOnStates[index] == isOn)
		{
			return;
		}

		_keyOnStates[index] = isOn;
		_ = SetLedInternalAsync(index, isOn ? ManagedOnBrightness : (byte)0, CancellationToken.None);
		EmitKeyEvent(button, isOn ? KeyEventType.KeyOn : KeyEventType.KeyOff, isPressed, isOn);
	}
}
