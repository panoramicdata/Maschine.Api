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
	private bool _buttonLedUnsupported;
	private EncoderTouchState _lastEncoderTouch;

	/// <inheritdoc/>
	public event EventHandler<KeyEvent>? KeyEvent;

	/// <inheritdoc/>
	public event EventHandler<EncoderTouchState>? EncoderTouchChanged;

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

	/// <inheritdoc/>
	public Task SetLedAsync(int buttonIndex, byte brightness, CancellationToken cancellationToken = default)
	{
		ThrowIfLibraryManaged(buttonIndex);
		SyncManagedKeyState(buttonIndex, brightness);
		return SetLedInternalAsync(buttonIndex, brightness, cancellationToken);
	}

	private Task SetLedInternalAsync(int buttonIndex, byte brightness, CancellationToken cancellationToken)
	{
		var scaled = _brightness.Scale(brightness);
		var report = MikroMk3Protocol.BuildButtonLedReport(buttonIndex, scaled);
		return WriteSingleButtonLedReportAsync(report, buttonIndex, scaled, cancellationToken);
	}

	/// <inheritdoc/>
	public Task SetAllLedsAsync(byte brightness, CancellationToken cancellationToken = default)
	{
		if (!_allowExternalLedOverrides && HasAnyLibraryManagedKeys())
		{
			throw new InvalidOperationException("One or more keys are in a managed key mode; individual/all-button LED writes are disabled for managed keys.");
		}

		SyncAllManagedKeyStates(brightness);
		return SetAllLedsInternalAsync(brightness, cancellationToken);
	}

	private Task SetAllLedsInternalAsync(byte brightness, CancellationToken cancellationToken)
	{
		var scaled = _brightness.Scale(brightness);
		var report = MikroMk3Protocol.BuildAllButtonLedsReport(scaled);
		return WriteAllButtonLedsReportAsync(report, scaled, cancellationToken);
	}

	internal Task SetAllLedsForShutdownAsync(byte brightness, CancellationToken cancellationToken = default)
		=> SetAllLedsInternalAsync(brightness, cancellationToken);

	/// <inheritdoc/>
	public Task SetOnOffAsync(int buttonIndex, bool isOn, CancellationToken cancellationToken = default)
		=> SetLedAsync(buttonIndex, isOn ? (byte)255 : (byte)0, cancellationToken);

	/// <inheritdoc/>
	public Task SetAllOnOffAsync(bool isOn, CancellationToken cancellationToken = default)
		=> SetAllLedsAsync(isOn ? (byte)255 : (byte)0, cancellationToken);

	private static void ValidateDirectLedButton(MikroMk3Button button)
	{
		if (!KeyModeDefaults.IsDirectLedKey(button))
		{
			throw new ArgumentOutOfRangeException(nameof(button), button, "Only keys with directly-addressable LEDs are supported by the key-mode engine.");
		}
	}

	private void ThrowIfLibraryManaged(int buttonIndex)
	{
		if (_allowExternalLedOverrides)
		{
			return;
		}

		if (buttonIndex < 0 || buttonIndex >= PhysicalButtonCount)
		{
			return;
		}

		if (!MikroMk3ButtonExtensions.TryFromIndex(buttonIndex, out var button) || !KeyModeDefaults.IsDirectLedKey(button))
		{
			return;
		}

		if (_keyModes[buttonIndex] != KeyMode.EventsOnly)
		{
			throw new InvalidOperationException($"LED for key '{button}' is managed by the library while mode '{_keyModes[buttonIndex]}' is active.");
		}
	}

	private void SyncManagedKeyState(int buttonIndex, byte brightness)
	{
		if (!_allowExternalLedOverrides
			|| buttonIndex < 0 || buttonIndex >= PhysicalButtonCount
			|| !MikroMk3ButtonExtensions.TryFromIndex(buttonIndex, out _)
			|| _keyModes[buttonIndex] == KeyMode.EventsOnly)
		{
			return;
		}

		_keyOnStates[buttonIndex] = brightness > 0;
	}

	private void SyncAllManagedKeyStates(byte brightness)
	{
		if (!_allowExternalLedOverrides)
		{
			return;
		}

		for (var i = 0; i < DirectLedButtonCount; i++)
		{
			if (_keyModes[i] != KeyMode.EventsOnly)
			{
				_keyOnStates[i] = brightness > 0;
			}
		}
	}

	private bool HasAnyLibraryManagedKeys()
	{
		for (var i = 0; i < DirectLedButtonCount; i++)
		{
			if (_keyModes[i] != KeyMode.EventsOnly)
			{
				return true;
			}
		}

		return false;
	}

	private async Task WriteSingleButtonLedReportAsync(byte[] report, int buttonIndex, byte brightness, CancellationToken cancellationToken)
	{
		if (_buttonLedUnsupported || _unifiedLights.IsEnabled)
		{
			await _unifiedLights.SetButtonBrightnessAsync(buttonIndex, brightness, cancellationToken)
				.ConfigureAwait(false);
			return;
		}

		try
		{
			await _device.WriteAsync(report, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (IsUnsupportedButtonLedError(ex))
		{
			_buttonLedUnsupported = true;
			_unifiedLights.Enable();
			await _unifiedLights.SetButtonBrightnessAsync(buttonIndex, brightness, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	private async Task WriteAllButtonLedsReportAsync(byte[] report, byte brightness, CancellationToken cancellationToken)
	{
		if (_buttonLedUnsupported || _unifiedLights.IsEnabled)
		{
			await _unifiedLights.SetAllButtonBrightnessAsync(brightness, cancellationToken).ConfigureAwait(false);
			return;
		}

		try
		{
			await _device.WriteAsync(report, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (IsUnsupportedButtonLedError(ex))
		{
			_buttonLedUnsupported = true;
			_unifiedLights.Enable();
			await _unifiedLights.SetAllButtonBrightnessAsync(brightness, cancellationToken).ConfigureAwait(false);
		}
	}

	private static bool IsUnsupportedButtonLedError(Exception ex)
	{
		for (Exception? current = ex; current is not null; current = current.InnerException)
		{
			var message = current.Message;
			if (message.Contains("parameter is incorrect", StringComparison.OrdinalIgnoreCase)
				|| message.Contains("SetFeature failed", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
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
			EncoderTouchChanged?.Invoke(this, touch);
		}
	}
}
