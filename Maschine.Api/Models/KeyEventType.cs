namespace Maschine.Api.Models;

/// <summary>
/// Logical key event emitted by the key-mode engine.
/// </summary>
public enum KeyEventType
{
	/// <summary>Physical key-down transition.</summary>
	KeyDown = 0,

	/// <summary>Physical key-up transition.</summary>
	KeyUp = 1,

	/// <summary>Logical key turned on.</summary>
	KeyOn = 2,

	/// <summary>Logical key turned off.</summary>
	KeyOff = 3,

	/// <summary>Momentary fire action.</summary>
	KeyPressed = 4,
}
