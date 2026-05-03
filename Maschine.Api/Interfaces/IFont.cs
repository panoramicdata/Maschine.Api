using System.Text;
using Maschine.Api.Models;

namespace Maschine.Api.Interfaces;

/// <summary>
/// Represents a bitmap font used by dot-matrix text widgets.
/// </summary>
/// <remarks>
/// Implementations can provide custom Unicode mappings by handling specific <see cref="Rune"/> values.
/// </remarks>
public interface IFont
{
	/// <summary>
	/// User-facing font name.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Base glyph height in pixels.
	/// </summary>
	int Height { get; }

	/// <summary>
	/// Indicates whether every glyph uses the same width.
	/// </summary>
	bool IsMonospace { get; }

	/// <summary>
	/// Fixed glyph width for monospace fonts; otherwise <see langword="null"/>.
	/// </summary>
	int? FixedWidth { get; }

	/// <summary>
	/// Tries to get bitmap glyph data for a Unicode rune.
	/// </summary>
	/// <param name="rune">Unicode scalar value to render.</param>
	/// <param name="glyph">Resolved glyph data when available.</param>
	/// <returns><see langword="true"/> when the rune is supported by this font.</returns>
	bool TryGetGlyph(Rune rune, out FontGlyph glyph);
}
