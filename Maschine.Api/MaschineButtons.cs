using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Threading;

namespace Maschine.Api;

/// <summary>
/// Manages button state and LED brightness for the Maschine Mikro MK3.
/// </summary>
internal sealed partial class MaschineButtons : IButtons
{
	private const int PhysicalButtonCount = 40;
	private const int DirectLedButtonCount = 39;
	private const byte ManagedOnBrightness = 255;

	private readonly IHidDevice _device;
	private readonly MikroMk3UnifiedLights _unifiedLights;
	private readonly LedBrightnessController _brightness;
	private readonly ButtonState[] _states;
	private readonly KeyMode[] _keyModes;
	private readonly bool[] _keyOnStates;
	private readonly int[] _groupByButton;
	private readonly RadioGroup[] _radioGroups;
	private readonly int _globalFireFlashDurationMs;
	private readonly int?[] _fireFlashDurationOverrideMs;
	private readonly int[] _flashGenerationByButton;
	private readonly bool[] _latchLongReleaseArmed;
	private readonly bool[] _latchShortReleaseArmed;
	private readonly bool _allowExternalLedOverrides;
	private EncoderTouchState _lastEncoderTouch;
	private EventHandler<KeyEvent>? _keyEvent;
	private EventHandler<EncoderTouchState>? _encoderTouchChanged;

	/// <inheritdoc/>
	public event EventHandler<KeyEvent>? KeyEvent
	{
		add => _keyEvent += value;
		remove => _keyEvent -= value;
	}

	/// <inheritdoc/>
	public event EventHandler<EncoderTouchState>? EncoderTouchChanged
	{
		add => _encoderTouchChanged += value;
		remove => _encoderTouchChanged -= value;
	}

	internal MaschineButtons(IHidDevice device, MikroMk3UnifiedLights unifiedLights, LedBrightnessController brightness, MaschineClientOptions options)
	{
		_device = device;
		_unifiedLights = unifiedLights;
		_brightness = brightness;
		_globalFireFlashDurationMs = Math.Max(0, options.KeyFireFlashDurationMs);
		_allowExternalLedOverrides = options.AllowExternalLedOverrides;
		_states = new ButtonState[MaschineDeviceConstants.MikroMk3ButtonCount];
		_keyModes = new KeyMode[PhysicalButtonCount];
		_keyOnStates = new bool[PhysicalButtonCount];
		_groupByButton = new int[PhysicalButtonCount];
		_fireFlashDurationOverrideMs = new int?[PhysicalButtonCount];
		_flashGenerationByButton = new int[PhysicalButtonCount];
		_latchLongReleaseArmed = new bool[PhysicalButtonCount];
		_latchShortReleaseArmed = new bool[PhysicalButtonCount];
		for (var i = 0; i < _states.Length; i++)
		{
			_states[i] = new ButtonState(i, false);
		}

		for (var i = 0; i < _groupByButton.Length; i++)
		{
			_groupByButton[i] = -1;
		}

		ConfigureKeyModes(options);
		ConfigureFlashOverrides(options);
		_radioGroups = ConfigureRadioGroups(options);
		InitializeRadioGroupDefaults();
	}

	/// <inheritdoc/>
	public IReadOnlyList<ButtonState> GetStates() => _states;

	/// <inheritdoc/>
	public ButtonState GetState(int buttonIndex)
	{
		if (buttonIndex < 0 || buttonIndex >= MaschineDeviceConstants.MikroMk3ButtonCount)
		{
			throw new ArgumentOutOfRangeException(nameof(buttonIndex), buttonIndex,
				$"Button index must be 0-{MaschineDeviceConstants.MikroMk3ButtonCount - 1}.");
		}

		return _states[buttonIndex];
	}

	/// <inheritdoc/>
	public bool IsKeyOn(MikroMk3Button button)
	{
		ValidateDirectLedButton(button);
		return _keyOnStates[(int)button];
	}

	/// <inheritdoc/>
	public bool IsKeyPressed(MikroMk3Button button)
	{
		ValidateDirectLedButton(button);
		return _states[(int)button].IsPressed;
	}

	private static void ValidateDirectLedButton(MikroMk3Button button)
	{
		if (!KeyModeDefaults.IsDirectLedKey(button))
		{
			throw new ArgumentOutOfRangeException(nameof(button), button, "Only keys with directly-addressable LEDs are supported by the key-mode engine.");
		}
	}

	/// <summary>
	/// Called by <see cref="MaschineClient"/> when a button report is received.
	/// Updates internal state and raises key and encoder events as needed.
	/// </summary>
	internal void ApplyReport(byte[] report)
	{
		if (report.Length == 0 || report[0] != MikroMk3Protocol.ButtonReportId)
		{
			return;
		}

		ApplyButtonBits(report);
		ApplyEncoderTouch(report);
	}

	private void ApplyButtonBits(byte[] report)
	{
		for (var i = 0; i < PhysicalButtonCount; i++)
		{
			var byteIndex = 1 + (i / 8);
			var bitIndex = i % 8;
			var isPressed = byteIndex < report.Length && ((report[byteIndex] >> bitIndex) & 1) == 1;

			if (_states[i].IsPressed == isPressed)
			{
				continue;
			}

			_states[i] = new ButtonState(i, isPressed);

			if (MikroMk3ButtonExtensions.TryFromIndex(i, out var button) && KeyModeDefaults.IsDirectLedKey(button))
			{
				ProcessKeyModeEvent(button, isPressed);
			}
		}
	}

	private void ApplyEncoderTouch(byte[] report)
	{
		// Parse encoder touch + absolute knob value
		if (report.Length < MikroMk3Protocol.ButtonReportLength)
		{
			return;
		}

		var touch = MikroMk3Protocol.ParseEncoderTouchFromButtonReport(report);
		if (touch != _lastEncoderTouch)
		{
			_lastEncoderTouch = touch;
			_encoderTouchChanged?.Invoke(this, touch);
		}
	}

	private void EmitKeyEvent(MikroMk3Button button, KeyEventType type, bool isPressed, bool isOn)
		=> _keyEvent?.Invoke(this, new KeyEvent(button, type, isPressed, isOn));
}
