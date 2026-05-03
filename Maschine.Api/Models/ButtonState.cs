namespace Maschine.Api.Models;

/// <summary>
/// Represents the state of a single button.
/// </summary>
public readonly record struct ButtonState
{
	/// <summary>
	/// Zero-based button index from HID input reports.
	/// </summary>
	public int Index { get; }

	/// <summary>
	/// True when the button is held down.
	/// </summary>
	public bool IsPressed { get; }

	/// <summary>
	/// Named button enum value when <see cref="Index"/> is a known physical button; otherwise <see langword="null"/>.
	/// </summary>
	public MikroMk3Button? Button { get; }

	/// <summary>
	/// Creates button state from a raw index and pressed state.
	/// </summary>
	/// <param name="index">Zero-based button index.</param>
	/// <param name="isPressed">True when the button is held down.</param>
	public ButtonState(int index, bool isPressed)
		: this(index, isPressed, MikroMk3ButtonExtensions.TryFromIndex(index, out var button) ? button : null)
	{
	}

	/// <summary>
	/// Creates button state from a raw index, pressed state, and explicit enum mapping.
	/// </summary>
	/// <param name="index">Zero-based button index.</param>
	/// <param name="isPressed">True when the button is held down.</param>
	/// <param name="button">Mapped enum value, or <see langword="null"/> for unmapped indices.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="button"/> does not match <paramref name="index"/>.</exception>
	public ButtonState(int index, bool isPressed, MikroMk3Button? button)
	{
		if (button.HasValue && (int)button.Value != index)
		{
			throw new ArgumentException("The enum button value must match the raw index.", nameof(button));
		}

		Index = index;
		IsPressed = isPressed;
		Button = button;
	}
}
