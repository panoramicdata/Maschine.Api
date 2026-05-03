namespace Maschine.Api.Test;

public sealed class DisplayBitmapRendererTests
{
	// ── PageBufferSize ────────────────────────────────────────────────────────

	[Fact]
	public void PageBufferSize_Is512()
		=> DisplayBitmapRenderer.PageBufferSize.Should().Be(512);

	// ── BitmapToPageBuffer ─────────────────────────────────────────────────────

	[Fact]
	public void BitmapToPageBuffer_WrongSize_Throws()
	{
		var bad = new byte[100];
		var act = () => DisplayBitmapRenderer.BitmapToPageBuffer(bad);
		act.Should().Throw<ArgumentException>().WithMessage("*512*");
	}

	[Fact]
	public void BitmapToPageBuffer_AllZero_ReturnsAllZeroBuffer()
	{
		var bitmap = new byte[512];
		var result = DisplayBitmapRenderer.BitmapToPageBuffer(bitmap);
		result.Should().HaveCount(512);
		result.Should().AllSatisfy(b => b.Should().Be(0));
	}

	[Fact]
	public void BitmapToPageBuffer_AllOnes_ReturnsAllOnesBuffer()
	{
		var bitmap = new byte[512];
		Array.Fill(bitmap, (byte)0xFF);
		var result = DisplayBitmapRenderer.BitmapToPageBuffer(bitmap);
		result.Should().AllSatisfy(b => b.Should().Be(0xFF));
	}

	[Fact]
	public void BitmapToPageBuffer_TopLeftPixel_SetsExpectedBit()
	{
		// Pixel (row=0, col=0) is bit 7 of bitmap[0].
		var bitmap = new byte[512];
		bitmap[0] = 0x80; // bit 7 = leftmost pixel of top row

		var result = DisplayBitmapRenderer.BitmapToPageBuffer(bitmap);

		// row=0 → page=0, bitInPage=0; col=0 → buffer[page*128+0] bit 0
		result[0].Should().Be(0x01);
	}

	[Fact]
	public void BitmapToPageBuffer_BottomRightPixel_SetsExpectedBit()
	{
		// Pixel (row=31, col=127) is bit 0 of bitmap[31*16+15] = bitmap[511].
		var bitmap = new byte[512];
		bitmap[511] = 0x01; // bit 0 = rightmost pixel of bottom row

		var result = DisplayBitmapRenderer.BitmapToPageBuffer(bitmap);

		// row=31 → page=3, bitInPage=7; col=127 → buffer[3*128+127] = buffer[511]
		result[511].Should().Be(0x80);
	}

	[Fact]
	public void BitmapToPageBuffer_Row8Col0_MapsToPage1Bit0()
	{
		// row=8 → page=1, bitInPage=0; col=0 → buffer[128]
		var bitmap = new byte[512];
		bitmap[8 * 16] = 0x80; // bit 7 = col 0

		var result = DisplayBitmapRenderer.BitmapToPageBuffer(bitmap);

		result[128].Should().Be(0x01, "row 8 = page 1, bit 0, column 0 → buffer[128] bit 0");
	}

	[Fact]
	public void BitmapToPageBuffer_WithPositiveOffsets_ShiftsPixel()
	{
		var bitmap = new byte[512];
		bitmap[0] = 0x80; // source pixel at row 0, col 0

		var result = DisplayBitmapRenderer.BitmapToPageBuffer(bitmap, xOffset: 1, yOffset: 1);

		// destination pixel at row 1, col 1 => page 0, bit 1, column 1
		result[1].Should().Be(0x02);
	}

	// ── SplitToSections ───────────────────────────────────────────────────────

	[Fact]
	public void SplitToSections_SplitsAtByte256()
	{
		var buf = new byte[512];
		for (var i = 0; i < 256; i++)
		{
			buf[i] = 0xAA;
		}

		for (var i = 256; i < 512; i++)
		{
			buf[i] = 0x55;
		}

		var (top, bottom) = DisplayBitmapRenderer.SplitToSections(buf);

		top.Should().HaveCount(256);
		bottom.Should().HaveCount(256);
		top.Should().AllSatisfy(b => b.Should().Be(0xAA));
		bottom.Should().AllSatisfy(b => b.Should().Be(0x55));
	}

	// ── TextToPageBuffer ──────────────────────────────────────────────────────

	[Fact]
	public void TextToPageBuffer_EmptyLines_ReturnsAllZero()
	{
		var result = DisplayBitmapRenderer.TextToPageBuffer([], DisplayLineMode.FourRows);
		result.Should().HaveCount(512);
		result.Should().AllSatisfy(b => b.Should().Be(0));
	}

	[Fact]
	public void TextToPageBuffer_FourRows_SpaceOnly_ReturnsAllZero()
	{
		var result = DisplayBitmapRenderer.TextToPageBuffer(["    "], DisplayLineMode.FourRows);
		result.Should().AllSatisfy(b => b.Should().Be(0));
	}

	[Fact]
	public void TextToPageBuffer_FourRows_SingleFullRow_IsNonZero()
	{
		// Any non-space printable character should produce at least one non-zero byte.
		var result = DisplayBitmapRenderer.TextToPageBuffer(["A"], DisplayLineMode.FourRows);
		result.Should().Contain(b => b != 0);
	}

	[Fact]
	public void TextToPageBuffer_OneRow_SingleChar_IsNonZero()
	{
		var result = DisplayBitmapRenderer.TextToPageBuffer(["A"], DisplayLineMode.OneRow);
		result.Should().Contain(b => b != 0);
	}

	[Fact]
	public void TextToPageBuffer_TwoRows_BothLines_BothHalvesNonZero()
	{
		var result = DisplayBitmapRenderer.TextToPageBuffer(["A", "B"], DisplayLineMode.TwoRows);

		// Top half (bytes 0-255, pages 0-1, rows 0-15)
		result[..256].Should().Contain(b => b != 0, "top line should produce pixels");

		// Bottom half (bytes 256-511, pages 2-3, rows 16-31)
		result[256..].Should().Contain(b => b != 0, "bottom line should produce pixels");
	}

	[Fact]
	public void TextToPageBuffer_EightRows_Line0_IsInPage0LowNibble()
	{
		// 8-line mode: line 0 → page 0, bits 0-3
		var result = DisplayBitmapRenderer.TextToPageBuffer(["A", "", "", "", "", "", "", ""], DisplayLineMode.EightRows);

		// At least one byte in page 0 (indices 0-127) should have bits 0-3 set
		var page0 = result[..128];
		page0.Should().Contain(b => (b & 0x0F) != 0);

		// And bits 4-7 should all be clear (line 1 is blank)
		page0.Should().AllSatisfy(b => (b & 0xF0).Should().Be(0));
	}

	[Fact]
	public void TextToPageBuffer_EightRows_Line1_IsInPage0HighNibble()
	{
		// 8-line mode: line 1 → page 0, bits 4-7
		var result = DisplayBitmapRenderer.TextToPageBuffer(["", "A", "", "", "", "", "", ""], DisplayLineMode.EightRows);

		var page0 = result[..128];
		page0.Should().Contain(b => (b & 0xF0) != 0);
		page0.Should().AllSatisfy(b => (b & 0x0F).Should().Be(0));
	}

	[Fact]
	public void TextToPageBuffer_ExtraLinesIgnored()
	{
		// Providing more lines than the mode supports should not throw.
		var manyLines = Enumerable.Range(0, 20).Select(i => $"Line{i}").ToList();
		var act = () => DisplayBitmapRenderer.TextToPageBuffer(manyLines, DisplayLineMode.FourRows);
		act.Should().NotThrow();
	}

	[Fact]
	public void TextToPageBuffer_WithNegativeXOffset_RevealsHiddenExtraColumn()
	{
		var visible = DisplayBitmapRenderer.TextToPageBuffer(["               A"], DisplayLineMode.FourRows);
		var scrolled = DisplayBitmapRenderer.TextToPageBuffer(["                A"], DisplayLineMode.FourRows, xOffset: -8);

		scrolled.Should().Equal(visible);
	}

	[Fact]
	public void TextToPageBuffer_WithNegativeYOffset_RevealsHiddenExtraRow()
	{
		var visible = DisplayBitmapRenderer.TextToPageBuffer(["", "", "", "A"], DisplayLineMode.FourRows);
		var scrolled = DisplayBitmapRenderer.TextToPageBuffer(["", "", "", "", "A"], DisplayLineMode.FourRows, yOffset: -8);

		scrolled.Should().Equal(visible);
	}
}
