namespace Maschine.Api.Models;

/// <summary>
/// Immutable monochrome glyph bitmap where each row byte stores pixels in LSB-left order.
/// </summary>
/// <param name="Width">Glyph width in pixels.</param>
/// <param name="Height">Glyph height in pixels.</param>
/// <param name="Rows">Row bytes (one byte per row, top-to-bottom).</param>
public sealed record FontGlyph(int Width, int Height, byte[] Rows)
{
	/// <summary>
	/// Creates a glyph after validating dimensions and row data.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when width or height is invalid.</exception>
	/// <exception cref="ArgumentException">Thrown when row data is malformed.</exception>
	public FontGlyph(int width, int height, IReadOnlyList<byte> rows)
		: this(width, height, rows.ToArray())
	{
		if (width <= 0 || width > 8)
		{
			throw new ArgumentOutOfRangeException(nameof(width), width, "Glyph width must be in range 1-8.");
		}

		if (height <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(height), height, "Glyph height must be at least 1.");
		}

		if (Rows.Length != height)
		{
			throw new ArgumentException($"Glyph row count must equal Height ({height}).", nameof(rows));
		}

		var mask = (1 << width) - 1;
		for (var i = 0; i < Rows.Length; i++)
		{
			if ((Rows[i] & ~mask) != 0)
			{
				throw new ArgumentException($"Row {i} contains bits outside glyph width {width}.", nameof(rows));
			}
		}
	}
}
