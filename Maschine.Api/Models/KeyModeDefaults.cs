namespace Maschine.Api.Models;

/// <summary>
/// Default key-mode profile provided by the library.
/// </summary>
public static class KeyModeDefaults
{
	private const int DirectLedKeyCount = 39;

	/// <summary>
	/// Returns a default key-mode map for LED-backed keys.
	/// </summary>
	public static Dictionary<MikroMk3Button, KeyMode> Create()
	{
		var map = new Dictionary<MikroMk3Button, KeyMode>();
		foreach (var button in Enum.GetValues<MikroMk3Button>())
		{
			if (IsDirectLedKey(button))
			{
				map[button] = KeyMode.LatchEarly;
			}
		}

		map[MikroMk3Button.Shift] = KeyMode.OnWhenPressed;
		map[MikroMk3Button.RestartLoop] = KeyMode.FireEarly;
		map[MikroMk3Button.LeftArrow] = KeyMode.FireEarly;
		map[MikroMk3Button.RightArrow] = KeyMode.FireEarly;
		return map;
	}

	/// <summary>
	/// Returns true when the key has a directly-addressable button LED slot.
	/// </summary>
	public static bool IsDirectLedKey(MikroMk3Button button)
		=> (int)button >= 0 && (int)button < DirectLedKeyCount;
}
