namespace Maschine.Api.Test;

public sealed class PadStateTests
{
	[Fact]
	public void IsPressed_WhenPressureIsZero_ReturnsFalse()
		=> new PadState(0, 0).IsPressed.Should().BeFalse();

	[Fact]
	public void IsPressed_WhenPressureIsPositive_ReturnsTrue()
		=> new PadState(0, 1).IsPressed.Should().BeTrue();

	[Fact]
	public void Index_IsStoredCorrectly()
		=> new PadState(7, 100).Index.Should().Be(7);

	[Fact]
	public void Pressure_IsStoredCorrectly()
		=> new PadState(0, 4095).Pressure.Should().Be(4095);

	[Fact]
	public void EqualStates_AreEqual()
		=> new PadState(3, 512).Should().Be(new PadState(3, 512));

	[Fact]
	public void DifferentStates_AreNotEqual()
		=> new PadState(3, 512).Should().NotBe(new PadState(3, 511));
}
