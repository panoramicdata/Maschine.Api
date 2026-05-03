namespace Maschine.Api.Models;

/// <summary>
/// Controls how many text rows are rendered across the 128×32 dot-matrix display.
/// </summary>
public enum DisplayLineMode
{
	/// <summary>
	/// One row of text spanning the full 32-pixel height (8 px-wide × 32 px-tall glyphs,
	/// 4× vertical scale). Fits up to 16 characters.
	/// </summary>
	OneRow = 1,

	/// <summary>
	/// Two rows of text, each 16 pixels tall (8 px-wide × 16 px-tall glyphs,
	/// 2× vertical scale). Fits up to 16 characters per row.
	/// </summary>
	TwoRows = 2,

	/// <summary>
	/// Four rows of text, each 8 pixels tall (standard 8×8 px glyphs, no scaling).
	/// Fits up to 16 characters per row.
	/// </summary>
	FourRows = 4,

	/// <summary>
	/// Eight rows of text, each 4 pixels tall (compact 4×4 px glyphs).
	/// Fits up to 32 characters per row.
	/// </summary>
	EightRows = 8,
}
