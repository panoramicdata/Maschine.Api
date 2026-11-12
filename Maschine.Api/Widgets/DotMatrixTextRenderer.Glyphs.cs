using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>Builds the glyph sequence for a line of text, substituting a placeholder for unsupported characters.</summary>
internal static partial class DotMatrixTextRenderer
{
	/// <summary>Glyphs drawn in place of a character the font does not provide.</summary>
	private static readonly Rune[] s_missingRunes = "[X]".EnumerateRunes().ToArray();

	private static List<FontGlyph> BuildGlyphs(string text, IFont font)
	{
		var glyphs = new List<FontGlyph>();
		foreach (var rune in text.EnumerateRunes())
		{
			if (font.TryGetGlyph(rune, out var glyph))
			{
				glyphs.Add(glyph);
				continue;
			}

			foreach (var fallbackRune in s_missingRunes)
			{
				if (font.TryGetGlyph(fallbackRune, out var fallbackGlyph))
				{
					glyphs.Add(fallbackGlyph);
				}
				else
				{
					glyphs.Add(CreateMissingGlyph(font));
				}
			}
		}

		return glyphs;
	}

	private static FontGlyph CreateMissingGlyph(IFont font)
	{
		var width = Math.Clamp(font.FixedWidth ?? 4, 3, 16);
		var height = Math.Max(3, font.Height);
		var rows = new ushort[height];
		var fullMask = (ushort)(width >= 16 ? 0xFFFF : ((1 << width) - 1));

		for (var y = 0; y < height; y++)
		{
			var row = (ushort)0;
			if (y == 0 || y == height - 1)
			{
				row = fullMask;
			}
			else
			{
				row |= 0x01;
				row |= (ushort)(1 << (width - 1));
				if (y == 1 || y == height - 2)
				{
					row |= (ushort)(1 << (width / 2));
				}
			}

			rows[y] = row;
		}

		return new FontGlyph(width, height, rows);
	}
}
