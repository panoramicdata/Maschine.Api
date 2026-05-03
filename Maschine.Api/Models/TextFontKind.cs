namespace Maschine.Api.Models;

/// <summary>
/// Built-in text font selection options for text widgets.
/// </summary>
public enum TextFontKind
{
	/// <summary>
	/// Automatically choose the best built-in font for the widget zone.
	/// </summary>
	Auto = 0,

	/// <summary>
	/// Use the proportional 4x4 font.
	/// </summary>
	Proportional4 = 1,

	/// <summary>
	/// Use the proportional 8x8 font.
	/// </summary>
	Proportional8 = 2,

	/// <summary>
	/// Use the bold proportional 8x8 font.
	/// </summary>
	Proportional8Bold = 3,

	/// <summary>
	/// Use the proportional 12px font.
	/// </summary>
	Proportional12 = 4,

	/// <summary>
	/// Use the bold proportional 12px font.
	/// </summary>
	Proportional12Bold = 5,
}
