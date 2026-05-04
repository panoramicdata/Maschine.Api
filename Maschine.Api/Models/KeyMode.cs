namespace Maschine.Api.Models;

/// <summary>
/// High-level behavior mode for a key in the button engine.
/// </summary>
public enum KeyMode
{
	/// <summary>Emit only physical edge events: KeyDown and KeyUp.</summary>
	EventsOnly = 0,

	/// <summary>Toggle on/off on key-down.</summary>
	LatchEarly = 1,

	/// <summary>Toggle on/off on key-up.</summary>
	LatchLate = 2,

	/// <summary>Turn on on key-down; turn off on the next key-up after the subsequent press cycle.</summary>
	LatchLong = 3,

	/// <summary>Turn on on key-up; turn off on the next key-down after the subsequent press cycle.</summary>
	LatchShort = 4,

	/// <summary>On while physically pressed, off when released.</summary>
	OnWhenPressed = 5,

	/// <summary>Emit KeyPressed on key-down and flash LED briefly.</summary>
	FireEarly = 6,

	/// <summary>Emit KeyPressed on key-up and flash LED briefly.</summary>
	FireLate = 7,
}
