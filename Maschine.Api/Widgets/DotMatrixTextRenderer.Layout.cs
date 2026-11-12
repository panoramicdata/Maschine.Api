using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>Clips, ellipsizes and pads glyph sequences to fit a zone.</summary>
internal static partial class DotMatrixTextRenderer
{
	private static List<FontGlyph> ClipGlyphs(List<FontGlyph> glyphs, int pixelWidth, ResolvedFont font)
	{
		var result = new List<FontGlyph>();
		var used = 0;
		foreach (var glyph in glyphs)
		{
			var width = glyph.Width * font.Scale;
			if (used + width > pixelWidth)
			{
				break;
			}

			result.Add(glyph);
			used += width;
		}

		return result;
	}

	private static List<FontGlyph> EllipsizeGlyphs(List<FontGlyph> glyphs, int pixelWidth, ResolvedFont font)
	{
		var ellipsis = BuildGlyphs("...", font.Font);
		var ellipsisWidth = MeasureWidth(ellipsis, font.Scale);
		if (ellipsisWidth > pixelWidth)
		{
			return ClipGlyphs(ellipsis, pixelWidth, font);
		}

		if (MeasureWidth(glyphs, font.Scale) <= pixelWidth)
		{
			return glyphs;
		}

		var head = new List<FontGlyph>();
		var used = 0;
		foreach (var glyph in glyphs)
		{
			var width = glyph.Width * font.Scale;
			if (used + width + ellipsisWidth > pixelWidth)
			{
				break;
			}

			head.Add(glyph);
			used += width;
		}

		head.AddRange(ellipsis);
		return head;
	}

	private static int MeasureWidth(List<FontGlyph> glyphs, int scale)
	{
		var width = 0;
		for (var i = 0; i < glyphs.Count; i++)
		{
			width += glyphs[i].Width * scale;
		}

		return width;
	}

	private static List<FontGlyph> AddScrollPadding(List<FontGlyph> glyphs, int spaces, ResolvedFont font)
	{
		if (spaces <= 0)
		{
			return glyphs;
		}

		var padded = new List<FontGlyph>(glyphs);
		for (var i = 0; i < spaces; i++)
		{
			if (font.Font.TryGetGlyph(new Rune(' '), out var space))
			{
				padded.Add(space);
			}
		}

		return padded;
	}
}
