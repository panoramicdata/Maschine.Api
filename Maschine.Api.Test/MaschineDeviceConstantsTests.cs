namespace Maschine.Api.Test;

public sealed class MaschineDeviceConstantsTests
{
	[Fact]
	public void VendorId_IsNativeInstruments()
		=> MaschineDeviceConstants.VendorId.Should().Be(0x17CC);

	[Fact]
	public void MikroMk3ProductId_Is0x1700()
		=> MaschineDeviceConstants.MikroMk3ProductId.Should().Be(0x1700);

	[Fact]
	public void MikroMk3PadCount_Is16()
		=> MaschineDeviceConstants.MikroMk3PadCount.Should().Be(16);

	[Fact]
	public void MikroMk3ButtonCount_Is45()
		=> MaschineDeviceConstants.MikroMk3ButtonCount.Should().Be(45);

	[Fact]
	public void MikroMk3EncoderCount_Is9()
		=> MaschineDeviceConstants.MikroMk3EncoderCount.Should().Be(9);
}
