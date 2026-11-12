using Maschine.Api.Internal;
using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Guards that stop external LED writes fighting the key-mode engine for <see cref="MaschineButtons"/>.
/// </summary>
internal sealed partial class MaschineButtons
{
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
}
