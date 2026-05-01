namespace Maschine.Api.Models;

/// <summary>
/// Represents the state of a single button.
/// </summary>
/// <param name="Index">Zero-based button index.</param>
/// <param name="IsPressed">True when the button is held down.</param>
public readonly record struct ButtonState(int Index, bool IsPressed);
