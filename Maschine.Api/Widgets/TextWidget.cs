using Maschine.Api.Models;
using Maschine.Api.Interfaces;

namespace Maschine.Api.Widgets;

/// <summary>
/// Text widget rendered with automatic best-fit font selection for its zone.
/// </summary>
public sealed class TextWidget : DotMatrixWidgetBase
{
	/// <summary>Text lines to render.</summary>
	public IReadOnlyList<string> Lines { get; set; }

	/// <summary>
	/// Overflow behavior used when text exceeds the available width.
	/// </summary>
	public TextOverflowMode OverflowMode { get; set; }

	/// <summary>
	/// Pixel offset used by scroll and rotate overflow modes.
	/// </summary>
	public int OverflowOffset { get; set; }

	/// <summary>
	/// Number of pixels to advance on each animation frame for scroll and rotate overflow modes.
	/// Use a negative value to move in the opposite direction.
	/// </summary>
	public int OverflowStepPixels { get; set; } = 1;

	/// <summary>
	/// Number of spaces inserted between repetitions in <see cref="TextOverflowMode.Scroll"/> mode.
	/// </summary>
	public int ScrollPadding { get; set; } = 3;

	/// <summary>
	/// Built-in font selection used when <see cref="Font"/> is not set.
	/// </summary>
	public TextFontKind FontKind { get; set; } = TextFontKind.ProportionalClassic;

	/// <summary>
	/// Optional custom font. When provided, this takes precedence over <see cref="FontKind"/>.
	/// </summary>
	public IFont? Font { get; set; }

	/// <summary>
	/// Creates a text widget.
	/// </summary>
	/// <param name="id">Stable widget identifier.</param>
	/// <param name="zone">Widget display zone in pixels.</param>
	/// <param name="lines">Text lines to render.</param>
	/// <param name="overflowMode">Overflow behavior for long lines.</param>
	/// <param name="invert">Whether to invert widget colors.</param>
	public TextWidget(string id, DisplayZone zone, IReadOnlyList<string> lines, TextOverflowMode overflowMode = TextOverflowMode.None, bool invert = false)
		: base(id, zone, invert)
	{
		Lines = lines ?? [];
		OverflowMode = overflowMode;
	}
}
