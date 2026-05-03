using Maschine.Api.Models;

namespace Maschine.Api.Internal;

/// <summary>
/// Converts bitmaps and text into the SSD1306-style page-format byte arrays
/// used by <see cref="MikroMk3DotMatrixDisplay"/>.
/// </summary>
/// <remarks>
/// <b>Display geometry</b> — 128 px wide × 32 px tall, four 8-row pages:
/// <list type="table">
///   <listheader><term>Page</term><description>Display rows</description></listheader>
///   <item><term>0</term><description>0–7 (top section, first half)</description></item>
///   <item><term>1</term><description>8–15 (top section, second half)</description></item>
///   <item><term>2</term><description>16–23 (bottom section, first half)</description></item>
///   <item><term>3</term><description>24–31 (bottom section, second half)</description></item>
/// </list>
/// Within each page byte, bit 0 = top row of the page, bit 7 = bottom row.
/// The page buffer is laid out as <c>buffer[page * 128 + column]</c>.
/// </remarks>
internal static class DisplayBitmapRenderer
{
	internal const int BitmapWidth = DisplayFont.DisplayWidth;
	internal const int BitmapHeight = DisplayFont.DisplayHeight;
	internal const int BitmapRowStride = BitmapWidth / 8;

	/// <summary>
	/// Total size in bytes of the full 4-page display buffer
	/// (<see cref="DisplayFont.TotalPages"/> × <see cref="DisplayFont.DisplayWidth"/>).
	/// </summary>
	internal const int PageBufferSize = DisplayFont.TotalPages * DisplayFont.DisplayWidth; // 512

	/// <summary>
	/// Converts a row-major packed bitmap into the page-format buffer expected by the display.
	/// </summary>
	/// <param name="bitmap">
	/// 512-byte packed bitmap: 32 rows × 16 bytes/row.
	/// <c>bitmap[row * 16 + col / 8]</c> bit <c>7 − (col % 8)</c> is the pixel at
	/// <c>(row, col)</c>; a set bit means a lit pixel.
	/// </param>
	/// <param name="xOffset">Signed pixel offset applied to the bitmap. Positive moves content right.</param>
	/// <param name="yOffset">Signed pixel offset applied to the bitmap. Positive moves content down.</param>
	/// <returns>512-byte page-format buffer (top 256 bytes then bottom 256 bytes).</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="bitmap"/> is not exactly 512 bytes.</exception>
	internal static byte[] BitmapToPageBuffer(byte[] bitmap, int xOffset = 0, int yOffset = 0)
	{
		const int expectedSize = BitmapHeight * BitmapRowStride; // 512
		if (bitmap.Length != expectedSize)
		{
			throw new ArgumentException(
				$"Bitmap must be {expectedSize} bytes ({BitmapWidth}×{BitmapHeight} packed).",
				nameof(bitmap));
		}

		var buf = new byte[PageBufferSize];

		for (var row = 0; row < BitmapHeight; row++)
		{
			for (var col = 0; col < BitmapWidth; col++)
			{
				var byteIndex = row * BitmapRowStride + col / 8;
				var bitIndex = 7 - (col % 8); // MSB = leftmost pixel

				if (((bitmap[byteIndex] >> bitIndex) & 1) != 0)
				{
					SetVisiblePixel(buf, col + xOffset, row + yOffset);
				}
			}
		}

		return buf;
	}

	/// <summary>
	/// Renders text lines into a page-format display buffer.
	/// </summary>
	/// <param name="lines">Text lines to render; extra lines beyond the mode capacity are ignored.</param>
	/// <param name="mode">Controls how many rows are rendered and which font/scale is used.</param>
	/// <param name="xOffset">Signed pixel offset applied to the text. Positive moves content right.</param>
	/// <param name="yOffset">Signed pixel offset applied to the text. Positive moves content down.</param>
	/// <returns>512-byte page-format buffer.</returns>
	internal static byte[] TextToPageBuffer(IReadOnlyList<string> lines, DisplayLineMode mode, int xOffset = 0, int yOffset = 0)
	{
		var buf = new byte[PageBufferSize];

		var rowCount = (int)mode;
		var lineCount = Math.Min(lines.Count, rowCount + 1);

		for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
		{
			var line = lines[lineIndex];

			if (mode == DisplayLineMode.EightRows)
			{
				RenderLine4x4(buf, line, lineIndex, xOffset, yOffset);
			}
			else
			{
				var verticalScale = DisplayFont.DisplayHeight / rowCount / DisplayFont.Font8Height; // 4, 2, or 1
				RenderLine8x8(buf, line, lineIndex, rowCount, verticalScale, xOffset, yOffset);
			}
		}

		return buf;
	}

	/// <summary>Splits a full page buffer into the top (pages 0-1) and bottom (pages 2-3) sections.</summary>
	internal static (byte[] top, byte[] bottom) SplitToSections(byte[] pageBuffer)
	{
		const int sectionSize = PageBufferSize / 2; // 256
		var top = new byte[sectionSize];
		var bottom = new byte[sectionSize];
		Buffer.BlockCopy(pageBuffer, 0, top, 0, sectionSize);
		Buffer.BlockCopy(pageBuffer, sectionSize, bottom, 0, sectionSize);
		return (top, bottom);
	}

	// ── Private helpers ───────────────────────────────────────────────────────

	/// <summary>
	/// Renders one text line using the 8×8 font at the given vertical scale.
	/// </summary>
	/// <param name="buf">Page-format output buffer (512 bytes).</param>
	/// <param name="line">Text to render; truncated to fit the display width.</param>
	/// <param name="lineIndex">Zero-based line index within the chosen mode.</param>
	/// <param name="rowCount">Total number of text rows in the mode.</param>
	/// <param name="verticalScale">Pixels per glyph row (1=4-line, 2=2-line, 4=1-line).</param>
	/// <param name="xOffset">Signed pixel offset applied to the text. Positive moves content right.</param>
	/// <param name="yOffset">Signed pixel offset applied to the text. Positive moves content down.</param>
	private static void RenderLine8x8(byte[] buf, string line, int lineIndex, int rowCount, int verticalScale, int xOffset, int yOffset)
	{
		var pixelsPerRow = DisplayFont.DisplayHeight / rowCount;             // 8, 16, or 32
		var startDisplayRow = lineIndex * pixelsPerRow + yOffset;
		var charsPerRow = (DisplayFont.DisplayWidth / DisplayFont.Font8Width) + 1; // 17

		var charCount = Math.Min(line.Length, charsPerRow);

		for (var charPos = 0; charPos < charCount; charPos++)
		{
			var glyph = DisplayFont.GetGlyph8x8(line[charPos]);
			var colStart = (charPos * DisplayFont.Font8Width) + xOffset;

			for (var glyphRow = 0; glyphRow < DisplayFont.Font8Height; glyphRow++)
			{
				var rowByte = glyph[glyphRow];

				for (var sy = 0; sy < verticalScale; sy++)
				{
					var displayRow = startDisplayRow + glyphRow * verticalScale + sy;
					if (displayRow >= DisplayFont.DisplayHeight)
					{
						break;
					}

					for (var cx = 0; cx < DisplayFont.Font8Width; cx++)
					{
						if (((rowByte >> cx) & 1) != 0)
						{
							SetVisiblePixel(buf, colStart + cx, displayRow);
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// Renders one text line using the 4×4 mini font (8-line mode).
	/// </summary>
	/// <param name="buf">Page-format output buffer (512 bytes).</param>
	/// <param name="line">Text to render; truncated to 32 characters.</param>
	/// <param name="lineIndex">Zero-based line index (0–7).</param>
	/// <param name="xOffset">Signed pixel offset applied to the text. Positive moves content right.</param>
	/// <param name="yOffset">Signed pixel offset applied to the text. Positive moves content down.</param>
	private static void RenderLine4x4(byte[] buf, string line, int lineIndex, int xOffset, int yOffset)
	{
		var startDisplayRow = (lineIndex * DisplayFont.Font4Height) + yOffset;
		var charsPerRow = DisplayFont.Font4CharsPerRow + 1; // 33
		var charCount = Math.Min(line.Length, charsPerRow);

		for (var charPos = 0; charPos < charCount; charPos++)
		{
			var glyph = DisplayFont.GetGlyph4x4(line[charPos]);
			var colStart = (charPos * DisplayFont.Font4Width) + xOffset;

			for (var glyphRow = 0; glyphRow < DisplayFont.Font4Height; glyphRow++)
			{
				var rowByte = glyph[glyphRow];
				var displayRow = startDisplayRow + glyphRow;

				for (var cx = 0; cx < DisplayFont.Font4Width; cx++)
				{
					if (((rowByte >> cx) & 1) != 0)
					{
						SetVisiblePixel(buf, colStart + cx, displayRow);
					}
				}
			}
		}
	}

	private static void SetVisiblePixel(byte[] buf, int displayX, int displayY)
	{
		if (displayX < 0 || displayX >= DisplayFont.DisplayWidth || displayY < 0 || displayY >= DisplayFont.DisplayHeight)
		{
			return;
		}

		var page = displayY / 8;
		var bitInPage = displayY % 8;
		buf[(page * DisplayFont.DisplayWidth) + displayX] |= (byte)(1 << bitInPage);
	}
}
