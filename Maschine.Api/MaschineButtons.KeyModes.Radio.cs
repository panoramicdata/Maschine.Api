using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Radio-group runtime behaviour for <see cref="MaschineButtons"/>.
/// </summary>
internal sealed partial class MaschineButtons
{
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
