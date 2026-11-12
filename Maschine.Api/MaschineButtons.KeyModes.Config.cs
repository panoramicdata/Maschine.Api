using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Validation and setup of the key-mode, flash-override and radio-group options for <see cref="MaschineButtons"/>.
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
}
