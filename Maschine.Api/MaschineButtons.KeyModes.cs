using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Key-mode engine for <see cref="MaschineButtons"/>: latch and fire behaviours, radio groups,
/// and the LED state they drive. Kept apart from the raw button/LED plumbing in the main file.
/// </summary>
internal sealed partial class MaschineButtons
{
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

			groups.Add(new RadioGroup(configured.Mode, ClaimGroupKeys(configured, i, nameof(options))));
		}

		return [.. groups];
	}

	/// <summary>
	/// Validates one configured radio group and records each of its keys as belonging to
	/// <paramref name="groupIndex"/>, returning the resolved button indices.
	/// </summary>
	/// <param name="configured">The group being validated.</param>
	/// <param name="groupIndex">Index of the group within the caller's collection.</param>
	/// <param name="paramName">
	/// Name of the caller's public parameter, so validation failures are reported against the
	/// argument the caller actually passed rather than against this private helper.
	/// </param>
	private int[] ClaimGroupKeys(RadioButtonGroupOptions configured, int groupIndex, string paramName)
	{
		var seen = new HashSet<int>();
		var keys = new int[configured.Keys.Count];
		for (var k = 0; k < configured.Keys.Count; k++)
		{
			var key = configured.Keys[k];
			if (!KeyModeDefaults.IsDirectLedKey(key))
			{
				throw new ArgumentException($"Radio group contains '{key}' which does not have a directly-addressable LED.", paramName);
			}

			var index = (int)key;
			if (!seen.Add(index))
			{
				throw new ArgumentException($"Radio group contains duplicate key '{key}'.", paramName);
			}

			if (_groupByButton[index] != -1)
			{
				throw new ArgumentException($"Key '{key}' appears in more than one radio group.", paramName);
			}

			_groupByButton[index] = groupIndex;
			keys[k] = index;
		}

		return keys;
	}

	private void InitializeRadioGroupDefaults()
	{
		for (var i = 0; i < _radioGroups.Length; i++)
		{
			var group = _radioGroups[i];
			if (group.Mode == RadioButtonGroupMode.AlwaysOneOn)
			{
				group.SelectedIndex = group.Keys[0];
				var idx = group.SelectedIndex.Value;
				_keyOnStates[idx] = true;
				_ = SetLedInternalAsync(idx, ManagedOnBrightness, CancellationToken.None);
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

	/// <summary>
	/// Applies a pending on/off change to the radio group owning <paramref name="index"/>, if any.
	/// Turning a key on turns its siblings off; turning the selected key of an
	/// <see cref="RadioButtonGroupMode.AlwaysOneOn"/> group off is refused.
	/// </summary>
	/// <returns><see langword="false"/> if the change must not proceed.</returns>
	private bool TryUpdateRadioGroup(int index, bool isOn)
	{
		var groupIndex = _groupByButton[index];
		if (groupIndex < 0)
		{
			return true;
		}

		var group = _radioGroups[groupIndex];
		if (!isOn)
		{
			if (group.SelectedIndex != index)
			{
				return true;
			}

			if (group.Mode == RadioButtonGroupMode.AlwaysOneOn)
			{
				return false;
			}

			group.SelectedIndex = null;
			return true;
		}

		DeselectGroupSiblings(group, index);
		group.SelectedIndex = index;
		return true;
	}

	private void DeselectGroupSiblings(RadioGroup group, int index)
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
