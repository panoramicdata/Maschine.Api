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
	/// Force a thinner fixed-width style.
	/// </summary>
	FixedThin = 1,

	/// <summary>
	/// Force the classic fixed-width 8x8 style.
	/// </summary>
	FixedClassic = 2,

	/// <summary>
	/// Use a proportional thin style.
	/// </summary>
	ProportionalThin = 3,

	/// <summary>
	/// Use a proportional classic 8x8 style.
	/// </summary>
	ProportionalClassic = 4,
}
