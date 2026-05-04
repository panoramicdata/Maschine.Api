using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Threading;

namespace Maschine.Api;

/// <summary>
/// Manages button state and LED brightness for the Maschine Mikro MK3.
/// </summary>
internal sealed class MaschineButtons : IButtons
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
		if (HasAnyLibraryManagedKeys())
		{
			throw new InvalidOperationException("One or more keys are in a managed key mode; individual/all-button LED writes are disabled for managed keys.");
		}

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

		for (var i = 0; i < PhysicalButtonCount; i++)
		{
			var byteIndex = 1 + (i / 8);
			var bitIndex = i % 8;
			var isPressed = byteIndex < report.Length && ((report[byteIndex] >> bitIndex) & 1) == 1;

			if (_states[i].IsPressed != isPressed)
			{
				_states[i] = new ButtonState(i, isPressed);

				if (MikroMk3ButtonExtensions.TryFromIndex(i, out var button) && KeyModeDefaults.IsDirectLedKey(button))
				{
					ProcessKeyModeEvent(button, isPressed);
				}
			}
		}

		// Parse encoder touch + absolute knob value
		if (report.Length >= MikroMk3Protocol.ButtonReportLength)
		{
			var touch = MikroMk3Protocol.ParseEncoderTouchFromButtonReport(report);
			if (touch != _lastEncoderTouch)
			{
				_lastEncoderTouch = touch;
				EncoderTouchChanged?.Invoke(this, touch);
			}
		}
	}

	private void ConfigureKeyModes(MaschineClientOptions options)
	{
		for (var i = 0; i < _keyModes.Length; i++)
		{
			_keyModes[i] = KeyMode.EventsOnly;
		}

		var keyModes = options.KeyModes ?? KeyModeDefaults.Create();
		foreach (var pair in keyModes)
		{
			if (!KeyModeDefaults.IsDirectLedKey(pair.Key))
			{
				throw new ArgumentException($"KeyModes contains '{pair.Key}' which does not have a directly-addressable LED.", nameof(options));
			}

			_keyModes[(int)pair.Key] = pair.Value;
		}
	}

	private void ConfigureFlashOverrides(MaschineClientOptions options)
	{
		if (options.KeyFireFlashDurationOverridesMs is null)
		{
			return;
		}

		foreach (var pair in options.KeyFireFlashDurationOverridesMs)
		{
			if (!KeyModeDefaults.IsDirectLedKey(pair.Key))
			{
				throw new ArgumentException($"KeyFireFlashDurationOverridesMs contains '{pair.Key}' which does not have a directly-addressable LED.", nameof(options));
			}

			_fireFlashDurationOverrideMs[(int)pair.Key] = pair.Value;
		}
	}

	private RadioGroup[] ConfigureRadioGroups(MaschineClientOptions options)
	{
		if (options.KeyRadioButtonGroups is null || options.KeyRadioButtonGroups.Count == 0)
		{
			return [];
		}

		var groups = new List<RadioGroup>(options.KeyRadioButtonGroups.Count);
		for (var i = 0; i < options.KeyRadioButtonGroups.Count; i++)
		{
			var configured = options.KeyRadioButtonGroups[i] ?? throw new ArgumentException("KeyRadioButtonGroups cannot contain null entries.", nameof(options));
			if (configured.Keys.Count == 0)
			{
				throw new ArgumentException("Radio button group cannot be empty.", nameof(options));
			}

			var seen = new HashSet<int>();
			var keys = new int[configured.Keys.Count];
			for (var k = 0; k < configured.Keys.Count; k++)
			{
				var key = configured.Keys[k];
				if (!KeyModeDefaults.IsDirectLedKey(key))
				{
					throw new ArgumentException($"Radio group contains '{key}' which does not have a directly-addressable LED.", nameof(options));
				}

				var index = (int)key;
				if (!seen.Add(index))
				{
					throw new ArgumentException($"Radio group contains duplicate key '{key}'.", nameof(options));
				}

				if (_groupByButton[index] != -1)
				{
					throw new ArgumentException($"Key '{key}' appears in more than one radio group.", nameof(options));
				}

				_groupByButton[index] = i;
				keys[k] = index;
			}

			groups.Add(new RadioGroup(configured.Mode, keys));
		}

		return [.. groups];
	}

	private void InitializeRadioGroupDefaults()
	{
		for (var i = 0; i < _radioGroups.Length; i++)
		{
			var group = _radioGroups[i];
			if (group.Mode == RadioButtonGroupMode.AlwaysOneOn)
			{
				group.SelectedIndex = group.Keys[0];
				_keyOnStates[group.SelectedIndex.Value] = true;
			}
		}
	}

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
				if (!_keyOnStates[index])
				{
					SetKeyOnState(button, true, true);
					_latchLongReleaseArmed[index] = false;
				}
				else
				{
					_latchLongReleaseArmed[index] = true;
				}
				break;

			case KeyMode.LatchShort:
				if (_keyOnStates[index])
				{
					SetKeyOnState(button, false, true);
					_latchShortReleaseArmed[index] = false;
				}
				else
				{
					_latchShortReleaseArmed[index] = true;
				}
				break;

			case KeyMode.OnWhenPressed:
				SetKeyOnState(button, true, true);
				break;

			case KeyMode.FireEarly:
				EmitKeyEvent(button, KeyEventType.KeyPressed, true, _keyOnStates[index]);
				ApplyFireActivation(button);
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
				if (_keyOnStates[index] && _latchLongReleaseArmed[index])
				{
					SetKeyOnState(button, false, false);
					_latchLongReleaseArmed[index] = false;
				}
				break;

			case KeyMode.LatchShort:
				if (!_keyOnStates[index] && _latchShortReleaseArmed[index])
				{
					SetKeyOnState(button, true, false);
					_latchShortReleaseArmed[index] = false;
				}
				break;

			case KeyMode.OnWhenPressed:
				SetKeyOnState(button, false, false);
				break;

			case KeyMode.FireLate:
				EmitKeyEvent(button, KeyEventType.KeyPressed, false, _keyOnStates[index]);
				ApplyFireActivation(button);
				break;
		}
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

	private void ActivateRadioGroupSelection(MikroMk3Button button)
	{
		var index = (int)button;
		var groupIndex = _groupByButton[index];
		if (groupIndex < 0)
		{
			return;
		}

		var group = _radioGroups[groupIndex];
		var isSelected = group.SelectedIndex == index;
		if (isSelected)
		{
			if (group.Mode == RadioButtonGroupMode.OneOrZeroOn)
			{
				SetKeyOnState(button, false, _states[index].IsPressed);
				group.SelectedIndex = null;
			}

			return;
		}

		if (group.SelectedIndex.HasValue)
		{
			SetKeyOnState((MikroMk3Button)group.SelectedIndex.Value, false, _states[group.SelectedIndex.Value].IsPressed);
		}

		SetKeyOnState(button, true, _states[index].IsPressed);
		group.SelectedIndex = index;
	}

	private void SetKeyOnState(MikroMk3Button button, bool isOn, bool isPressed)
	{
		var index = (int)button;
		var groupIndex = _groupByButton[index];
		if (groupIndex >= 0)
		{
			var group = _radioGroups[groupIndex];
			if (!isOn && group.SelectedIndex == index && group.Mode == RadioButtonGroupMode.AlwaysOneOn)
			{
				return;
			}

			if (isOn)
			{
				foreach (var otherIndex in group.Keys)
				{
					if (otherIndex == index || !_keyOnStates[otherIndex])
					{
						continue;
					}

					_keyOnStates[otherIndex] = false;
					_ = SetLedInternalAsync(otherIndex, 0, CancellationToken.None);
					EmitKeyEvent((MikroMk3Button)otherIndex, KeyEventType.KeyOff, _states[otherIndex].IsPressed, false);
				}

				group.SelectedIndex = index;
			}
			else if (group.SelectedIndex == index)
			{
				group.SelectedIndex = null;
			}
		}

		if (_keyOnStates[index] == isOn)
		{
			return;
		}

		_keyOnStates[index] = isOn;
		_ = SetLedInternalAsync(index, isOn ? ManagedOnBrightness : (byte)0, CancellationToken.None);
		EmitKeyEvent(button, isOn ? KeyEventType.KeyOn : KeyEventType.KeyOff, isPressed, isOn);
	}

	private void EmitKeyEvent(MikroMk3Button button, KeyEventType type, bool isPressed, bool isOn)
		=> KeyEvent?.Invoke(this, new KeyEvent(button, type, isPressed, isOn));

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

			if (_keyOnStates[index])
			{
				await SetLedInternalAsync(index, ManagedOnBrightness, CancellationToken.None).ConfigureAwait(false);
			}
			else
			{
				await SetLedInternalAsync(index, 0, CancellationToken.None).ConfigureAwait(false);
			}
		}
		catch
		{
			// Fire LED pulse is best-effort.
		}
	}

	private sealed class RadioGroup
	{
		internal RadioGroup(RadioButtonGroupMode mode, int[] keys)
		{
			Mode = mode;
			Keys = keys;
		}

		internal RadioButtonGroupMode Mode { get; }
		internal int[] Keys { get; }
		internal int? SelectedIndex { get; set; }
	}
}
