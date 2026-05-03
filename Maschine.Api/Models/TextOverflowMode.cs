namespace Maschine.Api.Models;

/// <summary>
/// Controls how text is handled when it exceeds the horizontal space available in a widget.
/// </summary>
public enum TextOverflowMode
{
	/// <summary>
	/// Truncate text to the visible width with no marker.
	/// </summary>
	None = 0,

	/// <summary>
	/// Replace the end of truncated text with an ellipsis.
	/// </summary>
	Ellipsis = 1,

	/// <summary>
	/// Scroll text through a padded gap of spaces before repeating.
	/// </summary>
	Scroll = 2,

	/// <summary>
	/// Rotate text continuously with no padded gap.
	/// </summary>
	Rotate = 3,
}
