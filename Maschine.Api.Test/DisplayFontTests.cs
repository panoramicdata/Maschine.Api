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
	{
		var glyph = DisplayFont.GetGlyph8x8(' ');
		foreach (var b in glyph)
		{
			b.Should().Be(0);
		}
	}

	[Fact]
	public void GetGlyph8x8_OutOfRangeLow_ReturnsSpaceGlyph()
	{
		// Characters below 0x20 should return the space glyph (all zeros)
		var space = DisplayFont.GetGlyph8x8(' ');
		var below = DisplayFont.GetGlyph8x8('\0');
		below.SequenceEqual(space).Should().BeTrue();
	}

	[Fact]
	public void GetGlyph8x8_OutOfRangeHigh_ReturnsSpaceGlyph()
	{
		// Characters above 0x7E should return the space glyph
		var space = DisplayFont.GetGlyph8x8(' ');
		var above = DisplayFont.GetGlyph8x8('\x80');
		above.SequenceEqual(space).Should().BeTrue();
	}

	[Fact]
	public void GetGlyph8x8_PrintableRange_AllHaveEightBytes()
	{
		for (var c = ' '; c <= '~'; c++)
		{
			DisplayFont.GetGlyph8x8(c).Length.Should().Be(8, $"glyph for '{c}' should have 8 bytes");
		}
	}

	[Theory]
	[InlineData('A')]
	[InlineData('Z')]
	[InlineData('0')]
	[InlineData('9')]
	public void GetGlyph8x8_AlphanumericChars_AreNonZero(char c)
	{
		// Alphanumeric characters should have at least one non-zero byte
		var glyph = DisplayFont.GetGlyph8x8(c);
		var hasPixels = false;
		foreach (var b in glyph)
		{
			if (b != 0)
			{
				hasPixels = true;
				break;
			}
		}

		hasPixels.Should().BeTrue($"glyph for '{c}' should have at least one lit pixel");
	}

	// ── Font4x4 ───────────────────────────────────────────────────────────────

	[Fact]
	public void Font4x4Glyphs_HasCorrectByteCount()
		// 95 printable ASCII characters × 4 bytes each
		=> DisplayFont.Font4x4Glyphs.Length.Should().Be(95 * 4);

	[Fact]
	public void GetGlyph4x4_Space_ReturnsAllZero()
	{
		var glyph = DisplayFont.GetGlyph4x4(' ');
		foreach (var b in glyph)
		{
			b.Should().Be(0);
		}
	}

	[Fact]
	public void GetGlyph4x4_OutOfRangeLow_ReturnsSpaceGlyph()
	{
		var space = DisplayFont.GetGlyph4x4(' ');
		var below = DisplayFont.GetGlyph4x4('\0');
		below.SequenceEqual(space).Should().BeTrue();
	}

	[Fact]
	public void GetGlyph4x4_PrintableRange_AllHaveFourBytes()
	{
		for (var c = ' '; c <= '~'; c++)
		{
			DisplayFont.GetGlyph4x4(c).Length.Should().Be(4, $"glyph for '{c}' should have 4 bytes");
		}
	}

	[Fact]
	public void GetGlyph4x4_AllRowBytesWithinFourBits()
	{
		// Each row byte should only use the lower 4 bits (columns 0-3)
		for (var c = ' '; c <= '~'; c++)
		{
			var glyph = DisplayFont.GetGlyph4x4(c);
			foreach (var b in glyph)
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
	{
		var glyph = DisplayFont.GetGlyph4x4(c);
		var hasPixels = false;
		foreach (var b in glyph)
		{
			if (b != 0)
			{
				hasPixels = true;
				break;
			}
		}

		hasPixels.Should().BeTrue($"glyph for '{c}' should have at least one lit pixel");
	}
}
