using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>
/// Rasterization half of <see cref="DotMatrixDashboard"/>: turns widgets into pixels in the
/// 1-bit display buffer. Kept apart from the widget-management API in the main file.
/// </summary>
public sealed partial class DotMatrixDashboard
{
	private static void RenderWidget(byte[] bitmap, IDotMatrixWidget widget)
	{
		var background = widget.Invert;
		var foreground = !background;

		FillZone(bitmap, widget.Zone, background);

		switch (widget)
		{
			case TextWidget text:
				RenderTextWidget(bitmap, text, foreground);
				break;
			case SpectrumWidget spectrum:
				RenderSpectrumWidget(bitmap, spectrum, foreground);
				break;
			case VuWidget vu:
				RenderVuWidget(bitmap, vu, foreground);
				break;
			default:
				// Unknown widget kinds render as their cleared background only.
				break;
		}
	}

	private static void FillZone(byte[] bitmap, DisplayZone zone, bool on)
	{
		for (var y = zone.Y; y < zone.Bottom; y++)
		{
			for (var x = zone.X; x < zone.Right; x++)
			{
				SetPixel(bitmap, x, y, on);
			}
		}
	}

	private static void RenderTextWidget(byte[] bitmap, TextWidget text, bool on)
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
						SetPixel(bitmap, zone, xBase + (col * scale) + sx, yBase + (row * scale) + sy, on);
					}
				}
			}
		}
	}

	private static void RenderSpectrumWidget(byte[] bitmap, SpectrumWidget spectrum, bool on)
	{
		var levels = spectrum.BandLevels;
		if (levels.Count == 0)
		{
			return;
		}

		var zone = spectrum.Zone;
		var bands = Math.Min(levels.Count, zone.Width);
		var gap = Math.Clamp(spectrum.GapPixels, 0, 8);
		for (var i = 0; i < bands; i++)
		{
			RenderSpectrumBand(bitmap, spectrum, i, bands, gap, on);
		}
	}

	private static void RenderSpectrumBand(byte[] bitmap, SpectrumWidget spectrum, int index, int bands, int gap, bool on)
	{
		var zone = spectrum.Zone;
		var slotStart = zone.X + (index * zone.Width) / bands;
		var slotEnd = zone.X + ((index + 1) * zone.Width) / bands;
		var x0 = slotStart + Math.Min(gap, Math.Max(0, (slotEnd - slotStart) - 1));
		var x1 = Math.Max(x0, slotEnd - 1);

		var level = Math.Clamp(spectrum.BandLevels[index], 0f, 1f);
		var barHeight = (int)Math.Round(level * zone.Height, MidpointRounding.AwayFromZero);
		FillRect(bitmap, x0, x1, zone.Bottom - barHeight, zone.Bottom - 1, on);

		var peaks = spectrum.PeakLevels;
		if (spectrum.ShowPeakMarkers && index < peaks.Count)
		{
			var markerY = zone.Bottom - 1 - (int)Math.Round(Math.Clamp(peaks[index], 0f, 1f) * (zone.Height - 1));
			FillRect(bitmap, x0, x1, markerY, markerY, on);
		}
	}

	/// <summary>Sets every pixel in the inclusive rectangle x0..x1 by y0..y1.</summary>
	private static void FillRect(byte[] bitmap, int x0, int x1, int y0, int y1, bool on)
	{
		for (var y = y0; y <= y1; y++)
		{
			for (var x = x0; x <= x1; x++)
			{
				SetPixel(bitmap, x, y, on);
			}
		}
	}

	private static void RenderVuWidget(byte[] bitmap, VuWidget vu, bool on)
	{
		if (vu.Style == VuWidgetStyle.Needle)
		{
			RenderNeedleVu(bitmap, vu, on);
			return;
		}

		RenderBarVu(bitmap, vu, on);
	}

	/// <summary>
	/// Draws a bar VU meter, growing left-to-right in a wide zone and bottom-to-top in a tall one.
	/// </summary>
	private static void RenderBarVu(byte[] bitmap, VuWidget vu, bool on)
	{
		if (vu.Zone.Width >= vu.Zone.Height)
		{
			RenderHorizontalBarVu(bitmap, vu, on);
			return;
		}

		RenderVerticalBarVu(bitmap, vu, on);
	}

	private static void RenderHorizontalBarVu(byte[] bitmap, VuWidget vu, bool on)
	{
		var zone = vu.Zone;
		var level = Math.Clamp(vu.Level, 0f, 1f);
		var width = (int)Math.Round(level * zone.Width, MidpointRounding.AwayFromZero);
		FillRect(bitmap, zone.X, zone.X + width - 1, zone.Y, zone.Bottom - 1, on);

		if (vu.ShowPeakMarker && vu.PeakLevel is float peak)
		{
			var markerX = zone.X + (int)Math.Round(Math.Clamp(peak, 0f, 1f) * (zone.Width - 1));
			FillRect(bitmap, markerX, markerX, zone.Y, zone.Bottom - 1, on);
		}
	}

	private static void RenderVerticalBarVu(byte[] bitmap, VuWidget vu, bool on)
	{
		var zone = vu.Zone;
		var level = Math.Clamp(vu.Level, 0f, 1f);
		var height = (int)Math.Round(level * zone.Height, MidpointRounding.AwayFromZero);
		FillRect(bitmap, zone.X, zone.Right - 1, zone.Bottom - height, zone.Bottom - 1, on);

		if (vu.ShowPeakMarker && vu.PeakLevel is float peak)
		{
			var markerY = zone.Bottom - 1 - (int)Math.Round(Math.Clamp(peak, 0f, 1f) * (zone.Height - 1));
			FillRect(bitmap, zone.X, zone.Right - 1, markerY, markerY, on);
		}
	}

	private static void RenderNeedleVu(byte[] bitmap, VuWidget vu, bool on)
	{
		var zone = vu.Zone;
		var centerX = zone.X + (zone.Width / 2);
		var centerY = zone.Bottom - 1;
		var radius = Math.Max(1, Math.Min((zone.Width / 2) - 1, zone.Height - 1));

		// Transform the logical gauge sweep so a bottom-pivot needle reads quiet-left to loud-right.
		var startAngle = 90.0 - vu.NeedleStartDegrees;
		var sweepAngle = -vu.NeedleSweepDegrees;

		if (ResolveNeedleDetailMode(vu, zone) == VuNeedleDetailMode.Detailed)
		{
			for (var t = 0; t <= 4; t++)
			{
				var angle = DegreesToRadians(startAngle + (t * (sweepAngle / 4.0)));
				var tx = centerX + (int)Math.Round(Math.Cos(angle) * radius);
				var ty = centerY - (int)Math.Round(Math.Sin(angle) * radius);
				SetPixel(bitmap, tx, ty, on);
			}
		}

		var levelAngle = DegreesToRadians(startAngle + (Math.Clamp(vu.Level, 0f, 1f) * sweepAngle));
		var x2 = centerX + (int)Math.Round(Math.Cos(levelAngle) * radius);
		var y2 = centerY - (int)Math.Round(Math.Sin(levelAngle) * radius);
		DrawLine(bitmap, centerX, centerY, x2, y2, on);

		if (vu.ShowPeakMarker && vu.PeakLevel is float peak)
		{
			var peakAngle = DegreesToRadians(startAngle + (Math.Clamp(peak, 0f, 1f) * sweepAngle));
			RenderNeedlePeakArc(bitmap, centerX, centerY, radius, peakAngle, on);
		}
	}

	private static void RenderNeedlePeakArc(byte[] bitmap, int centerX, int centerY, int radius, double peakAngle, bool on)
	{
		const double HalfSpanRadians = Math.PI / 48.0; // ~3.75° each side of the peak
		var arcStart = peakAngle - HalfSpanRadians;
		var arcEnd = peakAngle + HalfSpanRadians;
		var angleDelta = arcEnd - arcStart;
		var steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(angleDelta) / (Math.PI / 180.0))); // ~1° steps

		for (var i = 0; i <= steps; i++)
		{
			var t = i / (double)steps;
			var angle = arcStart + (angleDelta * t);
			var x = centerX + (int)Math.Round(Math.Cos(angle) * radius);
			var y = centerY - (int)Math.Round(Math.Sin(angle) * radius);
			SetPixel(bitmap, x, y, on);
		}
	}

	private static VuNeedleDetailMode ResolveNeedleDetailMode(VuWidget vu, DisplayZone zone)
	{
		if (vu.NeedleDetailMode != VuNeedleDetailMode.Auto)
		{
			return vu.NeedleDetailMode;
		}

		return zone.Width >= 16 && zone.Height >= 8
			? VuNeedleDetailMode.Detailed
			: VuNeedleDetailMode.Simple;
	}

	private static double DegreesToRadians(double degrees) => (Math.PI / 180.0) * degrees;

	private static void DrawLine(byte[] bitmap, int x0, int y0, int x1, int y1, bool on)
	{
		var dx = Math.Abs(x1 - x0);
		var sx = x0 < x1 ? 1 : -1;
		var dy = -Math.Abs(y1 - y0);
		var sy = y0 < y1 ? 1 : -1;
		var err = dx + dy;

		while (true)
		{
			SetPixel(bitmap, x0, y0, on);
			if (x0 == x1 && y0 == y1)
			{
				break;
			}

			var e2 = 2 * err;
			if (e2 >= dy)
			{
				err += dy;
				x0 += sx;
			}

			if (e2 <= dx)
			{
				err += dx;
				y0 += sy;
			}
		}
	}

	private static void SetPixel(byte[] bitmap, DisplayZone zone, int x, int y, bool on)
	{
		if (x < zone.X || x >= zone.Right || y < zone.Y || y >= zone.Bottom)
		{
			return;
		}

		SetPixel(bitmap, x, y, on);
	}

	private static void SetPixel(byte[] bitmap, int x, int y, bool on)
	{
		if (x < 0 || x >= DisplayWidth || y < 0 || y >= DisplayHeight)
		{
			return;
		}

		var index = (y * RowStride) + (x / 8);
		var bit = 7 - (x % 8);
		var mask = (byte)(1 << bit);
		if (on)
		{
			bitmap[index] |= mask;
		}
		else
		{
			bitmap[index] &= (byte)~mask;
		}
	}

	private readonly record struct ResolvedFont(IFont Font, int Scale)
	{
		public int PixelHeight => Font.Height * Scale;
	}
}
