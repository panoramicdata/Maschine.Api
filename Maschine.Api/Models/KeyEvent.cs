namespace Maschine.Api.Models;

/// <summary>
/// Key event raised by the key-mode engine.
/// </summary>
public readonly record struct KeyEvent
{
	/// <summary>The key this event applies to.</summary>
	public MikroMk3Button Button { get; }

	/// <summary>The event type.</summary>
	public KeyEventType Type { get; }

	/// <summary>Physical pressed state after this transition.</summary>
	public bool IsPressed { get; }

	/// <summary>Logical on/off state after this transition.</summary>
	public bool IsOn { get; }

	/// <summary>
	/// Creates a key event.
	/// </summary>
	public KeyEvent(MikroMk3Button button, KeyEventType type, bool isPressed, bool isOn)
	{
		Button = button;
		Type = type;
		IsPressed = isPressed;
		IsOn = isOn;
	}
}
