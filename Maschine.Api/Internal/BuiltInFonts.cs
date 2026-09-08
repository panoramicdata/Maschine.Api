using System.Text;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;

namespace Maschine.Api.Internal;

internal static class BuiltInFonts
{
	internal static readonly IFont Proportional4 =
		new ProportionalFont("Proportional4", DisplayFont.Font4Height, 4, 2,
			c => ToUShortRows(DisplayFont.GetGlyph4x4(c)));

	internal static readonly IFont Proportional8 =
		new ProportionalFont("Proportional8", DisplayFont.Font8Height, 8, 4,
			c => ToUShortRows(DisplayFont.GetGlyph8x8Light(c)));

	internal static readonly IFont Proportional8Bold =
		new ProportionalFont("Proportional8Bold", DisplayFont.Font8Height, 8, 4,
			c => ToUShortRows(DisplayFont.GetGlyph8x8(c)));

	internal static readonly IFont Proportional12 =
		new ProportionalFont("Proportional12", DisplayFont.Font12Height, 16, 5,
			c => DisplayFont.GetGlyph12Regular(c).ToArray());

	internal static readonly IFont Proportional12Bold =
		new ProportionalFont("Proportional12Bold", DisplayFont.Font12Height, 16, 5,
			c => DisplayFont.GetGlyph12Bold(c).ToArray());

	/// <summary>
	/// A built-in proportional font over printable ASCII. Each glyph is looked up as a fixed-width
	/// bitmap and then trimmed to its inked width, so only the space character needs an explicit
	/// width of its own.
	/// </summary>
	/// <param name="name">Font name reported by <see cref="IFont.Name"/>.</param>
	/// <param name="height">Glyph height in pixels.</param>
	/// <param name="maxBits">Width of the untrimmed glyph bitmap, in bits.</param>
	/// <param name="spaceWidth">Rendered width of the space character, which has no ink to measure.</param>
	/// <param name="lookup">Returns the untrimmed bitmap rows for a character.</param>
	private sealed class ProportionalFont(
		string name,
		int height,
		int maxBits,
		int spaceWidth,
		Func<char, ushort[]> lookup) : IFont
	{
		public string Name => name;
		public int Height => height;
		public bool IsMonospace => false;
		public int? FixedWidth => null;

		public bool TryGetGlyph(Rune rune, out FontGlyph glyph)
		{
			if (rune.Value < 0x20 || rune.Value > 0x7E)
			{
				glyph = default!;
				return false;
			}

			var rows = lookup((char)rune.Value);
			var width = rune.Value == 0x20 ? spaceWidth : ComputeTrimmedWidth(rows, maxBits);
			glyph = new FontGlyph(width, height, TrimRows(rows, width));
			return true;
		}
	}

	private static int ComputeTrimmedWidth(ushort[] rows, int maxBits)
	{
		var highestSetBit = -1;
		for (var r = 0; r < rows.Length; r++)
		{
			for (var bit = maxBits - 1; bit >= 0; bit--)
			{
				if (((rows[r] >> bit) & 1) != 0)
				{
					highestSetBit = Math.Max(highestSetBit, bit);
					break;
				}
			}
		}

		return highestSetBit < 0 ? 1 : highestSetBit + 1;
	}

	private static ushort[] TrimRows(ushort[] rows, int width)
	{
		var mask = width >= 16 ? 0xFFFF : (1 << width) - 1;
		var trimmed = new ushort[rows.Length];
		for (var i = 0; i < rows.Length; i++)
		{
			trimmed[i] = (ushort)(rows[i] & mask);
		}

		return trimmed;
	}

	private static ushort[] ToUShortRows(ReadOnlySpan<byte> rows)
	{
		var widened = new ushort[rows.Length];
		for (var i = 0; i < rows.Length; i++)
		{
			widened[i] = rows[i];
		}

		return widened;
	}
}
