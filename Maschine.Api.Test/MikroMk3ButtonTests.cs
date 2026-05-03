namespace Maschine.Api.Test;

public sealed class MikroMk3ButtonTests
{
	[Fact]
	public void Values_MapToProvidedHardwareIndices()
	{
		((int)MikroMk3Button.MachineLogo).Should().Be(0);
		((int)MikroMk3Button.Star).Should().Be(1);
		((int)MikroMk3Button.Search).Should().Be(2);
		((int)MikroMk3Button.VolumeVelocity).Should().Be(3);
		((int)MikroMk3Button.Swing).Should().Be(4);
		((int)MikroMk3Button.TempoTune).Should().Be(5);
		((int)MikroMk3Button.PlugInMicrok8s).Should().Be(6);
		((int)MikroMk3Button.Sampling).Should().Be(7);
		((int)MikroMk3Button.LeftArrow).Should().Be(8);
		((int)MikroMk3Button.RightArrow).Should().Be(9);
		((int)MikroMk3Button.Pitch).Should().Be(10);
		((int)MikroMk3Button.Mod).Should().Be(11);
		((int)MikroMk3Button.PerformFxSelect).Should().Be(12);
		((int)MikroMk3Button.Notes).Should().Be(13);
		((int)MikroMk3Button.Group).Should().Be(14);
		((int)MikroMk3Button.Auto).Should().Be(15);
		((int)MikroMk3Button.Lock).Should().Be(16);
		((int)MikroMk3Button.NoteRepeatArp).Should().Be(17);
		((int)MikroMk3Button.RestartLoop).Should().Be(18);
		((int)MikroMk3Button.ArraysReplace).Should().Be(19);
		((int)MikroMk3Button.Tapro).Should().Be(20);
		((int)MikroMk3Button.FollowGrid).Should().Be(21);
		((int)MikroMk3Button.Play).Should().Be(22);
		((int)MikroMk3Button.RecCountIn).Should().Be(23);
		((int)MikroMk3Button.Stop).Should().Be(24);
		((int)MikroMk3Button.Shift).Should().Be(25);
		((int)MikroMk3Button.FixedVel16Vel).Should().Be(26);
		((int)MikroMk3Button.PadMode).Should().Be(27);
		((int)MikroMk3Button.Keyboard).Should().Be(28);
		((int)MikroMk3Button.Chords).Should().Be(29);
		((int)MikroMk3Button.Step).Should().Be(30);
		((int)MikroMk3Button.SceneSelection).Should().Be(31);
		((int)MikroMk3Button.Pattern).Should().Be(32);
		((int)MikroMk3Button.Events).Should().Be(33);
		((int)MikroMk3Button.VariationNavigate).Should().Be(34);
		((int)MikroMk3Button.DuplicateDouble).Should().Be(35);
		((int)MikroMk3Button.Select).Should().Be(36);
		((int)MikroMk3Button.Solo).Should().Be(37);
		((int)MikroMk3Button.MuteChoke).Should().Be(38);
		((int)MikroMk3Button.KnobPress).Should().Be(39);
	}

	[Fact]
	public void EveryValue_HasHumanFriendlyDisplayName()
	{
		foreach (var button in Enum.GetValues<MikroMk3Button>())
		{
			button.GetDisplayName().Should().NotBeNullOrWhiteSpace();
		}
	}

	[Theory]
	[InlineData(MikroMk3Button.MachineLogo, "MACHINE Logo")]
	[InlineData(MikroMk3Button.NoteRepeatArp, "NOTE REPEAT\nArp")]
	[InlineData(MikroMk3Button.FixedVel16Vel, "FIXED VEL\n16 Vel")]
	[InlineData(MikroMk3Button.KnobPress, "Knob Press")]
	public void DisplayName_UsesAttributeValue(MikroMk3Button button, string expected)
	{
		button.GetDisplayName().Should().Be(expected);
	}
}
