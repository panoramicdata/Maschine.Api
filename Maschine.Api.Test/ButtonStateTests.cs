namespace Maschine.Api.Test;

public sealed class ButtonStateTests
{
	[Fact]
	public void IsPressed_True_WhenSet()
		=> new ButtonState(0, true).IsPressed.Should().BeTrue();

	[Fact]
	public void IsPressed_False_WhenNotSet()
		=> new ButtonState(0, false).IsPressed.Should().BeFalse();

	[Fact]
	public void Index_IsStoredCorrectly()
		=> new ButtonState(42, false).Index.Should().Be(42);

	[Fact]
	public void EqualStates_AreEqual()
		=> new ButtonState(5, true).Should().Be(new ButtonState(5, true));

	[Fact]
	public void DifferentStates_AreNotEqual()
		=> new ButtonState(5, true).Should().NotBe(new ButtonState(5, false));

	[Fact]
	public void KnownIndex_MapsToEnum()
	{
		var state = new ButtonState(0, true);

		state.Button.Should().Be(MikroMk3Button.MachineLogo);
	}

	[Fact]
	public void UnknownIndex_HasNoEnumMapping()
	{
		var state = new ButtonState(44, true);

		state.Button.Should().BeNull();
	}

	[Fact]
	public void MismatchedExplicitEnum_Throws()
	{
		var act = () => new ButtonState(1, true, MikroMk3Button.MachineLogo);

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void DisplayNameSingleLine_ReplacesLineBreaks()
	{
		var text = MikroMk3Button.VolumeVelocity.GetDisplayNameSingleLine();

		text.Should().Be("VOLUME / [Velocity]");
	}
}
