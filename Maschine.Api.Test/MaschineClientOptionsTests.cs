namespace Maschine.Api.Test;

public sealed class MaschineClientOptionsTests
{
	[Fact]
	public void DefaultVendorId_IsNativeInstruments()
		=> new MaschineClientOptions().VendorId.Should().Be(MaschineDeviceConstants.VendorId);

	[Fact]
	public void DefaultProductId_IsMikroMk3()
		=> new MaschineClientOptions().ProductId.Should().Be(MaschineDeviceConstants.MikroMk3ProductId);

	[Fact]
	public void DefaultDeviceIndex_IsZero()
		=> new MaschineClientOptions().DeviceIndex.Should().Be(0);

	[Fact]
	public void Properties_CanBeSet()
	{
		var opts = new MaschineClientOptions { VendorId = 1, ProductId = 2, DeviceIndex = 1 };
		opts.VendorId.Should().Be(1);
		opts.ProductId.Should().Be(2);
		opts.DeviceIndex.Should().Be(1);
	}
}
