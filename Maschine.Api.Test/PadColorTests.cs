namespace Maschine.Api.Test;

public sealed class PadColorTests
{
	[Fact]
	public void Off_HasAllZeroComponents()
	{
		PadColor.Off.R.Should().Be(0);
		PadColor.Off.G.Should().Be(0);
		PadColor.Off.B.Should().Be(0);
	}

	[Fact]
	public void White_HasAllMaxComponents()
	{
		PadColor.White.R.Should().Be(255);
		PadColor.White.G.Should().Be(255);
		PadColor.White.B.Should().Be(255);
	}

	[Fact]
	public void Red_HasOnlyRedComponent()
	{
		PadColor.Red.R.Should().Be(255);
		PadColor.Red.G.Should().Be(0);
		PadColor.Red.B.Should().Be(0);
	}

	[Fact]
	public void Green_HasOnlyGreenComponent()
	{
		PadColor.Green.R.Should().Be(0);
		PadColor.Green.G.Should().Be(255);
		PadColor.Green.B.Should().Be(0);
	}

	[Fact]
	public void Blue_HasOnlyBlueComponent()
	{
		PadColor.Blue.R.Should().Be(0);
		PadColor.Blue.G.Should().Be(0);
		PadColor.Blue.B.Should().Be(255);
	}

	[Fact]
	public void Constructor_SetsComponents()
	{
		var color = new PadColor(10, 20, 30);
		color.R.Should().Be(10);
		color.G.Should().Be(20);
		color.B.Should().Be(30);
	}

	[Fact]
	public void EqualColors_AreEqual()
		=> new PadColor(1, 2, 3).Should().Be(new PadColor(1, 2, 3));

	[Fact]
	public void DifferentColors_AreNotEqual()
		=> new PadColor(1, 2, 3).Should().NotBe(new PadColor(1, 2, 4));
}
