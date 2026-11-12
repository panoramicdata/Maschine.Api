using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>
/// Lays out and draws text widgets: font selection for the zone, glyph building, the
/// overflow modes, and scaling each glyph onto the buffer.
/// </summary>
internal static partial class DotMatrixTextRenderer
{
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

	private readonly record struct ResolvedFont(IFont Font, int Scale)
	{
		public int PixelHeight => Font.Height * Scale;
	}
}
