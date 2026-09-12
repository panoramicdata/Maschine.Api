using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>
/// Lays out and draws text widgets: font selection for the zone, glyph building, the
/// overflow modes, and scaling each glyph onto the buffer.
/// </summary>
internal static class DotMatrixTextRenderer
{
	/// <summary>Glyphs drawn in place of a character the font does not provide.</summary>
	private static readonly Rune[] s_missingRunes = "[X]".EnumerateRunes().ToArray();

	internal static void RenderTextWidget(byte[] bitmap, TextWidget text, bool on)
	{
		var lines = text.Lines ?? [];
		if (lines.Count == 0)
		{
			return;
		}

		var resolvedFont = ResolveTextFont(text, text.Zone, lines.Count);
		var maxLines = Math.Max(1, text.Zone.Height / resolvedFont.PixelHeight);
		var lineCount = Math.Min(lines.Count, maxLines);

		var overflow = new TextOverflowSettings(text.OverflowMode, text.OverflowOffset, text.ScrollPadding);
		for (var line = 0; line < lineCount; line++)
		{
			var textLine = lines[line] ?? string.Empty;
			RenderTextLine(bitmap, text.Zone, resolvedFont, line, textLine, overflow, on);
		}
	}

	private static ResolvedFont ResolveTextFont(TextWidget text, DisplayZone zone, int requestedLineCount)
	{
		if (text.Font is IFont custom)
		{
			return ScaleFontToZone(custom, zone, requestedLineCount);
		}

		var font = text.FontKind switch
		{
			TextFontKind.Auto => SelectAutoFont(zone, requestedLineCount),
			TextFontKind.Proportional8 => BuiltInFonts.Proportional8,
			TextFontKind.Proportional8Bold => BuiltInFonts.Proportional8Bold,
			TextFontKind.Proportional12 => BuiltInFonts.Proportional12,
			TextFontKind.Proportional12Bold => BuiltInFonts.Proportional12Bold,
			_ => BuiltInFonts.Proportional4,
		};

		return ScaleFontToZone(font, zone, requestedLineCount);
	}

	/// <summary>
	/// Picks the largest built-in font whose unscaled height still fits the requested number of
	/// lines in the zone.
	/// </summary>
	private static IFont SelectAutoFont(DisplayZone zone, int requestedLineCount)
	{
		var targetLines = Math.Max(1, requestedLineCount);
		if (zone.Height >= (DisplayFont.Font12Height * targetLines))
		{
			return BuiltInFonts.Proportional12;
		}

		return zone.Height >= (DisplayFont.Font8Height * targetLines)
			? BuiltInFonts.Proportional8
			: BuiltInFonts.Proportional4;
	}

	private static ResolvedFont ScaleFontToZone(IFont font, DisplayZone zone, int requestedLineCount)
	{
		var targetLines = Math.Max(1, requestedLineCount);
		var maxScaleByHeight = Math.Max(1, zone.Height / (font.Height * targetLines));
		return new ResolvedFont(font, maxScaleByHeight);
	}

	/// <summary>Overflow settings shared by every line of one text widget.</summary>
	private readonly record struct TextOverflowSettings(TextOverflowMode Mode, int OffsetPixels, int Padding);

	private static void RenderTextLine(byte[] bitmap, DisplayZone zone, ResolvedFont font, int lineIndex, string text, TextOverflowSettings overflow, bool on)
	{
		var overflowMode = overflow.Mode;
		var sourceGlyphs = BuildGlyphs(text ?? string.Empty, font.Font);
		var yBase = zone.Y + (lineIndex * font.PixelHeight);
		if (yBase >= zone.Bottom)
		{
			return;
		}

		var lineGlyphs = overflowMode switch
		{
			TextOverflowMode.Ellipsis => EllipsizeGlyphs(sourceGlyphs, zone.Width, font),
			TextOverflowMode.None => ClipGlyphs(sourceGlyphs, zone.Width, font),
			_ => sourceGlyphs,
		};

		if (overflowMode is TextOverflowMode.Scroll or TextOverflowMode.Rotate)
		{
			var cycleGlyphs = overflowMode == TextOverflowMode.Scroll
				? AddScrollPadding(lineGlyphs, Math.Max(1, overflow.Padding), font)
				: lineGlyphs;

			RenderCycledGlyphs(bitmap, zone, yBase, cycleGlyphs, font, overflow.OffsetPixels, on);
			return;
		}

		RenderClippedGlyphs(bitmap, zone, yBase, lineGlyphs, font, on);
	}

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

	private readonly record struct ResolvedFont(IFont Font, int Scale)
	{
		public int PixelHeight => Font.Height * Scale;
	}
}
