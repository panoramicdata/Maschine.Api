using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>Draws laid-out glyphs onto the display bitmap.</summary>
internal static partial class DotMatrixTextRenderer
{
	private static void RenderCycledGlyphs(byte[] bitmap, DisplayZone zone, int yBase, List<FontGlyph> glyphs, ResolvedFont font, int offsetPixels, bool on)
	{
		if (glyphs.Count == 0)
		{
			return;
		}

		var widths = glyphs.Select(g => g.Width * font.Scale).ToArray();
		var cycleWidth = widths.Sum();
		if (cycleWidth <= 0)
		{
			return;
		}

		var wrappedOffset = Mod(offsetPixels, cycleWidth);
		var startIndex = 0;
		while (startIndex < widths.Length && wrappedOffset >= widths[startIndex])
		{
			wrappedOffset -= widths[startIndex];
			startIndex++;
		}

		if (startIndex >= glyphs.Count)
		{
			startIndex = 0;
			wrappedOffset = 0;
		}

		var x = zone.X - wrappedOffset;
		var index = startIndex;
		while (x < zone.Right)
		{
			var glyph = glyphs[index];
			RenderGlyph(bitmap, zone, glyph, x, yBase, font.Scale, on);
			x += widths[index];
			index = (index + 1) % glyphs.Count;
		}
	}

	private static int Mod(int value, int modulo)
		=> modulo == 0 ? 0 : ((value % modulo) + modulo) % modulo;

	private static void RenderClippedGlyphs(byte[] bitmap, DisplayZone zone, int yBase, List<FontGlyph> glyphs, ResolvedFont font, bool on)
	{
		var x = zone.X;
		for (var i = 0; i < glyphs.Count; i++)
		{
			var glyph = glyphs[i];
			RenderGlyph(bitmap, zone, glyph, x, yBase, font.Scale, on);
			x += glyph.Width * font.Scale;
			if (x >= zone.Right)
			{
				break;
			}
		}
	}

	private static void RenderGlyph(byte[] bitmap, DisplayZone zone, FontGlyph glyph, int xBase, int yBase, int scale, bool on)
	{
		for (var row = 0; row < glyph.Height; row++)
		{
			var rowBits = glyph.Rows[row];
			for (var col = 0; col < glyph.Width; col++)
			{
				if (((rowBits >> col) & 1) == 0)
				{
					continue;
				}

				for (var sy = 0; sy < scale; sy++)
				{
					for (var sx = 0; sx < scale; sx++)
					{
						DotMatrixCanvas.SetPixel(bitmap, zone, xBase + (col * scale) + sx, yBase + (row * scale) + sy, on);
					}
				}
			}
		}
	}
}
