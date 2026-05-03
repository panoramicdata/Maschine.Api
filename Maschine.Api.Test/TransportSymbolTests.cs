namespace Maschine.Api.Test;

public sealed class TransportSymbolTests
{
	[Theory]
	[InlineData(TransportSymbol.Play, 0x25B6)]
	[InlineData(TransportSymbol.Pause, 0x23F8)]
	[InlineData(TransportSymbol.Stop, 0x23F9)]
	[InlineData(TransportSymbol.Rewind, 0x23EA)]
	[InlineData(TransportSymbol.FastForward, 0x23E9)]
	[InlineData(TransportSymbol.Record, 0x23FA)]
	public void EnumValues_MatchUnicodeCodePoints(TransportSymbol symbol, int expectedCodePoint)
	{
		((int)symbol).Should().Be(expectedCodePoint);
	}

	[Fact]
	public void ToRune_ReturnsMatchingUnicodeScalar()
	{
		TransportSymbol.Play.ToRune().Value.Should().Be(0x25B6);
		TransportSymbol.Stop.ToRune().Value.Should().Be(0x23F9);
	}
}
