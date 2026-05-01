namespace Maschine.Api.Test;

public sealed class EncoderDeltaTests
{
	[Fact]
	public void Index_IsStoredCorrectly()
		=> new EncoderDelta(3, 1).Index.Should().Be(3);

	[Fact]
	public void Delta_Positive_IsCW()
		=> new EncoderDelta(0, 5).Delta.Should().BePositive();

	[Fact]
	public void Delta_Negative_IsCCW()
		=> new EncoderDelta(0, -3).Delta.Should().BeNegative();

	[Fact]
	public void EqualDeltas_AreEqual()
		=> new EncoderDelta(1, 2).Should().Be(new EncoderDelta(1, 2));

	[Fact]
	public void DifferentDeltas_AreNotEqual()
		=> new EncoderDelta(1, 2).Should().NotBe(new EncoderDelta(1, -2));
}
