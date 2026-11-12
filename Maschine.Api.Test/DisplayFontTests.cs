namespace Maschine.Api.Test;

public sealed class DisplayFontTests
{
	// ── Font8x8 ───────────────────────────────────────────────────────────────

	[Fact]
	public void Font8x8Glyphs_HasCorrectByteCount()
		// 95 printable ASCII characters × 8 bytes each
		=> DisplayFont.Font8x8Glyphs.Length.Should().Be(95 * 8);

	[Fact]
	public void GetGlyph8x8_Space_ReturnsAllZero()
		=> AssertAllZero(DisplayFont.GetGlyph8x8(' '));

	[Fact]
	public void GetGlyph8x8_OutOfRangeLow_ReturnsSpaceGlyph()
		// Characters below 0x20 should return the space glyph (all zeros)
		=> AssertSameAsSpace(DisplayFont.GetGlyph8x8('\0'), DisplayFont.GetGlyph8x8(' '));

	[Fact]
	public void GetGlyph8x8_OutOfRangeHigh_ReturnsSpaceGlyph()
		// Characters above 0x7E should return the space glyph
		=> AssertSameAsSpace(DisplayFont.GetGlyph8x8('\x80'), DisplayFont.GetGlyph8x8(' '));

	[Fact]
	public void GetGlyph8x8_PrintableRange_AllHaveEightBytes()
		=> AssertPrintableGlyphLengths(c => DisplayFont.GetGlyph8x8(c).Length, 8);

	[Theory]
	[InlineData('A')]
	[InlineData('Z')]
	[InlineData('0')]
	[InlineData('9')]
	public void GetGlyph8x8_AlphanumericChars_AreNonZero(char c)
		=> AssertHasLitPixel(DisplayFont.GetGlyph8x8(c), c);

	// ── Font4x4 ───────────────────────────────────────────────────────────────

	[Fact]
	public void Font4x4Glyphs_HasCorrectByteCount()
		// 95 printable ASCII characters × 4 bytes each
		=> DisplayFont.Font4x4Glyphs.Length.Should().Be(95 * 4);

	[Fact]
	public void GetGlyph4x4_Space_ReturnsAllZero()
		=> AssertAllZero(DisplayFont.GetGlyph4x4(' '));

	[Fact]
	public void GetGlyph4x4_OutOfRangeLow_ReturnsSpaceGlyph()
		=> AssertSameAsSpace(DisplayFont.GetGlyph4x4('\0'), DisplayFont.GetGlyph4x4(' '));

	[Fact]
	public void GetGlyph4x4_PrintableRange_AllHaveFourBytes()
		=> AssertPrintableGlyphLengths(c => DisplayFont.GetGlyph4x4(c).Length, 4);

	[Fact]
	public void GetGlyph4x4_AllRowBytesWithinFourBits()
	{
		// Each row byte should only use the lower 4 bits (columns 0-3)
		for (var c = ' '; c <= '~'; c++)
		{
			foreach (var b in DisplayFont.GetGlyph4x4(c))
			{
				(b & 0xF0).Should().Be(0, $"glyph row for '{c}' must not set bits above bit 3");
			}
		}
	}

	[Theory]
	[InlineData('A')]
	[InlineData('0')]
	[InlineData('Z')]
	public void GetGlyph4x4_AlphanumericChars_AreNonZero(char c)
		=> AssertHasLitPixel(DisplayFont.GetGlyph4x4(c), c);

	// ── Font12 (Helv Regular/Bold) ──────────────────────────────────────────

	[Fact]
	public void Font12RegularGlyphs_HasCorrectWordCount()
		=> DisplayFont.Font12x12HelvRegularGlyphs.Length.Should().Be(95 * DisplayFont.Font12Height);

	[Fact]
	public void Font12BoldGlyphs_HasCorrectWordCount()
		=> DisplayFont.Font12x12HelvBoldGlyphs.Length.Should().Be(95 * DisplayFont.Font12Height);

	[Fact]
	public void GetGlyph12Regular_PrintableRange_AllHaveTwelveRows()
		=> AssertPrintableGlyphLengths(c => DisplayFont.GetGlyph12Regular(c).Length, DisplayFont.Font12Height);

	[Fact]
	public void GetGlyph12Bold_PrintableRange_AllHaveTwelveRows()
		=> AssertPrintableGlyphLengths(c => DisplayFont.GetGlyph12Bold(c).Length, DisplayFont.Font12Height);

	// ── Helpers ─────────────────────────────────────────────────────────────

	private static void AssertAllZero(ReadOnlySpan<byte> glyph)
	{
		foreach (var b in glyph)
		{
			b.Should().Be(0);
		}
	}

	private static void AssertSameAsSpace(ReadOnlySpan<byte> glyph, ReadOnlySpan<byte> space)
		=> glyph.SequenceEqual(space).Should().BeTrue();

	private static void AssertHasLitPixel(ReadOnlySpan<byte> glyph, char c)
		=> (glyph.IndexOfAnyExcept((byte)0) >= 0).Should().BeTrue($"glyph for '{c}' should have at least one lit pixel");

	private static void AssertPrintableGlyphLengths(Func<char, int> lengthOf, int expected)
	{
		for (var c = ' '; c <= '~'; c++)
		{
			lengthOf(c).Should().Be(expected, $"glyph for '{c}' should have {expected} rows or bytes");
		}
	}
}
