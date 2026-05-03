using Maschine.Api.Internal;
using Maschine.Api.Exceptions;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>
/// Widget-based compositor for the 128x32 dot-matrix display.
/// Widgets are stacked in insertion order and must not overlap.
/// </summary>
public sealed class DotMatrixDashboard
{
	private const int DisplayWidth = DisplayFont.DisplayWidth;
	private const int DisplayHeight = DisplayFont.DisplayHeight;
	private const int RowStride = DisplayWidth / 8;
	private static readonly Rune[] s_missingRunes = "[X]".EnumerateRunes().ToArray();

	private readonly List<IDotMatrixWidget> _widgets = [];

	/// <summary>Raised when a widget is added.</summary>
	public event EventHandler<IDotMatrixWidget>? WidgetAdded;
	/// <summary>Raised when a widget is updated or reordered.</summary>
	public event EventHandler<IDotMatrixWidget>? WidgetUpdated;
	/// <summary>Raised when a widget is removed.</summary>
	public event EventHandler<IDotMatrixWidget>? WidgetRemoved;
	/// <summary>Raised when all widgets are cleared.</summary>
	public event EventHandler? WidgetsCleared;
	/// <summary>Raised after a bitmap is composed.</summary>
	public event EventHandler? Rendered;

	/// <summary>Current widget stack in render order.</summary>
	public IReadOnlyList<IDotMatrixWidget> Widgets => _widgets;

	/// <summary>
	/// Adds a widget to the stack.
	/// </summary>
	/// <param name="widget">Widget to add.</param>
	public void AddWidget(IDotMatrixWidget widget)
	{
		ArgumentNullException.ThrowIfNull(widget);
		ValidateZone(widget.Zone);
		EnsureNoOverlap(widget);

		if (_widgets.Any(w => string.Equals(w.Id, widget.Id, StringComparison.Ordinal)))
		{
			throw new InvalidOperationException($"A widget with id '{widget.Id}' already exists.");
		}

		_widgets.Add(widget);
		WidgetAdded?.Invoke(this, widget);
	}

	/// <summary>
	/// Updates a widget in place.
	/// </summary>
	/// <param name="id">Widget id.</param>
	/// <param name="update">Mutation callback.</param>
	public void UpdateWidget(string id, Action<IDotMatrixWidget> update)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(update);

		var widget = _widgets.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.Ordinal))
			?? throw new InvalidOperationException($"Widget '{id}' not found.");

		update(widget);
		ValidateZone(widget.Zone);
		EnsureNoOverlap(widget);
		WidgetUpdated?.Invoke(this, widget);
	}

	/// <summary>
	/// Removes a widget by id.
	/// </summary>
	/// <param name="id">Widget id.</param>
	/// <returns><see langword="true"/> when removed; otherwise <see langword="false"/>.</returns>
	public bool RemoveWidget(string id)
	{
		var index = _widgets.FindIndex(w => string.Equals(w.Id, id, StringComparison.Ordinal));
		if (index < 0)
		{
			return false;
		}

		var removed = _widgets[index];
		_widgets.RemoveAt(index);
		WidgetRemoved?.Invoke(this, removed);
		return true;
	}

	/// <summary>
	/// Removes all widgets.
	/// </summary>
	public void ClearWidgets()
	{
		if (_widgets.Count == 0)
		{
			return;
		}

		_widgets.Clear();
		WidgetsCleared?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Moves a widget to the top of the render stack.
	/// </summary>
	/// <param name="id">Widget id.</param>
	public void BringToFront(string id)
	{
		MoveWidget(id, _widgets.Count - 1);
	}

	/// <summary>
	/// Moves a widget to the bottom of the render stack.
	/// </summary>
	/// <param name="id">Widget id.</param>
	public void SendToBack(string id)
	{
		MoveWidget(id, 0);
	}

	/// <summary>
	/// Composes all widgets into a 512-byte row-packed bitmap.
	/// </summary>
	/// <returns>Bitmap in 32 rows x 16 bytes/row packed format.</returns>
	public byte[] BuildBitmap()
	{
		var bitmap = new byte[DisplayHeight * RowStride];
		foreach (var widget in _widgets)
		{
			RenderWidget(bitmap, widget);
		}

		Rendered?.Invoke(this, EventArgs.Empty);
		return bitmap;
	}

	/// <summary>
	/// Advances animated widget state by one frame.
	/// </summary>
	/// <param name="direction">Animation direction. Positive advances forward; negative advances backward.</param>
	/// <returns><see langword="true"/> when at least one widget state changed.</returns>
	public bool AdvanceFrame(int direction = 1)
	{
		if (direction == 0)
		{
			return false;
		}

		var changed = false;
		foreach (var widget in _widgets)
		{
			if (widget is TextWidget text
				&& (text.OverflowMode == TextOverflowMode.Scroll || text.OverflowMode == TextOverflowMode.Rotate)
				&& text.OverflowStepPixels != 0)
			{
				text.OverflowOffset += text.OverflowStepPixels * Math.Sign(direction);
				changed = true;
			}
		}

		return changed;
	}

	private void MoveWidget(string id, int newIndex)
	{
		var oldIndex = _widgets.FindIndex(w => string.Equals(w.Id, id, StringComparison.Ordinal));
		if (oldIndex < 0)
		{
			throw new InvalidOperationException($"Widget '{id}' not found.");
		}

		newIndex = Math.Clamp(newIndex, 0, _widgets.Count - 1);
		if (oldIndex == newIndex)
		{
			return;
		}

		var item = _widgets[oldIndex];
		_widgets.RemoveAt(oldIndex);
		_widgets.Insert(newIndex, item);
		WidgetUpdated?.Invoke(this, item);
	}

	private static void ValidateZone(DisplayZone zone)
	{
		if (!zone.IsWithin(DisplayWidth, DisplayHeight))
		{
			throw new ArgumentOutOfRangeException(nameof(zone), zone,
				$"Zone must be fully within {DisplayWidth}x{DisplayHeight} display bounds.");
		}
	}

	private void EnsureNoOverlap(IDotMatrixWidget candidate)
	{
		foreach (var existing in _widgets)
		{
			if (ReferenceEquals(existing, candidate))
			{
				continue;
			}

			if (existing.Zone.Intersects(candidate.Zone))
			{
				throw new DashboardLayoutException(
					$"Widget '{candidate.Id}' overlaps existing widget '{existing.Id}'.");
			}
		}
	}

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

		for (var line = 0; line < lineCount; line++)
		{
			var textLine = lines[line] ?? string.Empty;
			RenderTextLine(bitmap, text.Zone, resolvedFont, line, textLine, text.OverflowMode, text.OverflowOffset, text.ScrollPadding, on);
		}
	}

	private static ResolvedFont ResolveTextFont(TextWidget text, DisplayZone zone, int requestedLineCount)
	{
		if (text.Font is IFont custom)
		{
			return ScaleFontToZone(custom, zone, requestedLineCount);
		}

		if (text.FontKind == TextFontKind.Auto)
		{
			var targetLines = Math.Max(1, requestedLineCount);
			var classicFits = zone.Height >= (BuiltInFonts.ProportionalClassic.Height * targetLines);
			return ScaleFontToZone(classicFits ? BuiltInFonts.ProportionalClassic : BuiltInFonts.ProportionalThin, zone, requestedLineCount);
		}

		if (text.FontKind == TextFontKind.FixedClassic)
		{
			return ScaleFontToZone(BuiltInFonts.FixedClassic, zone, requestedLineCount);
		}

		if (text.FontKind == TextFontKind.FixedThin)
		{
			return ScaleFontToZone(BuiltInFonts.FixedThin, zone, requestedLineCount);
		}

		if (text.FontKind == TextFontKind.ProportionalClassic)
		{
			return ScaleFontToZone(BuiltInFonts.ProportionalClassic, zone, requestedLineCount);
		}

		return ScaleFontToZone(BuiltInFonts.ProportionalThin, zone, requestedLineCount);
	}

	private static ResolvedFont ScaleFontToZone(IFont font, DisplayZone zone, int requestedLineCount)
	{
		var targetLines = Math.Max(1, requestedLineCount);
		var maxScaleByHeight = Math.Max(1, zone.Height / (font.Height * targetLines));
		return new ResolvedFont(font, maxScaleByHeight);
	}

	private static void RenderTextLine(byte[] bitmap, DisplayZone zone, ResolvedFont font, int lineIndex, string text, TextOverflowMode overflowMode, int offsetPixels, int padding, bool on)
	{
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
				? AddScrollPadding(lineGlyphs, Math.Max(1, padding), font)
				: lineGlyphs;

			RenderCycledGlyphs(bitmap, zone, yBase, cycleGlyphs, font, offsetPixels, on);
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
		var width = Math.Clamp(font.FixedWidth ?? 4, 3, 8);
		var height = Math.Max(3, font.Height);
		var rows = new byte[height];
		var fullMask = (byte)((1 << width) - 1);

		for (var y = 0; y < height; y++)
		{
			var row = (byte)0;
			if (y == 0 || y == height - 1)
			{
				row = fullMask;
			}
			else
			{
				row |= 0x01;
				row |= (byte)(1 << (width - 1));
				if (y == 1 || y == height - 2)
				{
					row |= (byte)(1 << (width / 2));
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
			var rowByte = glyph.Rows[row];
			for (var col = 0; col < glyph.Width; col++)
			{
				if (((rowByte >> col) & 1) == 0)
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
		var peaks = spectrum.PeakLevels;
		for (var i = 0; i < bands; i++)
		{
			var slotStart = zone.X + (i * zone.Width) / bands;
			var slotEnd = zone.X + ((i + 1) * zone.Width) / bands;
			var x0 = slotStart + Math.Min(gap, Math.Max(0, (slotEnd - slotStart) - 1));
			var x1 = slotEnd - 1;
			if (x1 < x0)
			{
				x1 = x0;
			}

			var level = Math.Clamp(levels[i], 0f, 1f);
			var barHeight = (int)Math.Round(level * zone.Height, MidpointRounding.AwayFromZero);
			for (var y = zone.Bottom - 1; y >= zone.Bottom - barHeight; y--)
			{
				for (var x = x0; x <= x1; x++)
				{
					SetPixel(bitmap, x, y, on);
				}
			}

			if (spectrum.ShowPeakMarkers && i < peaks.Count)
			{
				var markerY = zone.Bottom - 1 - (int)Math.Round(Math.Clamp(peaks[i], 0f, 1f) * (zone.Height - 1));
				for (var x = x0; x <= x1; x++)
				{
					SetPixel(bitmap, x, markerY, on);
				}
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

	private static void RenderBarVu(byte[] bitmap, VuWidget vu, bool on)
	{
		var zone = vu.Zone;
		var level = Math.Clamp(vu.Level, 0f, 1f);
		if (zone.Width >= zone.Height)
		{
			var width = (int)Math.Round(level * zone.Width, MidpointRounding.AwayFromZero);
			for (var x = zone.X; x < zone.X + width; x++)
			{
				for (var y = zone.Y; y < zone.Bottom; y++)
				{
					SetPixel(bitmap, x, y, on);
				}
			}

			if (vu.ShowPeakMarker && vu.PeakLevel is float peak)
			{
				var markerX = zone.X + (int)Math.Round(Math.Clamp(peak, 0f, 1f) * (zone.Width - 1));
				for (var y = zone.Y; y < zone.Bottom; y++)
				{
					SetPixel(bitmap, markerX, y, on);
				}
			}
		}
		else
		{
			var height = (int)Math.Round(level * zone.Height, MidpointRounding.AwayFromZero);
			for (var y = zone.Bottom - 1; y >= zone.Bottom - height; y--)
			{
				for (var x = zone.X; x < zone.Right; x++)
				{
					SetPixel(bitmap, x, y, on);
				}
			}

			if (vu.ShowPeakMarker && vu.PeakLevel is float peak)
			{
				var markerY = zone.Bottom - 1 - (int)Math.Round(Math.Clamp(peak, 0f, 1f) * (zone.Height - 1));
				for (var x = zone.X; x < zone.Right; x++)
				{
					SetPixel(bitmap, x, markerY, on);
				}
			}
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
			var px = centerX + (int)Math.Round(Math.Cos(peakAngle) * radius);
			var py = centerY - (int)Math.Round(Math.Sin(peakAngle) * radius);
			SetPixel(bitmap, px, py, on);
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
