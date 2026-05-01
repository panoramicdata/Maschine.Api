namespace Maschine.Api.Test;

public sealed class DeviceInfoTests
{
	[Fact]
	public void Properties_AreStoredCorrectly()
	{
		var info = new DeviceInfo(0x17CC, 0x1700, "1.0", "ABC123");
		info.VendorId.Should().Be(0x17CC);
		info.ProductId.Should().Be(0x1700);
		info.FirmwareVersion.Should().Be("1.0");
		info.SerialNumber.Should().Be("ABC123");
	}

	[Fact]
	public void NullOptionalFields_AreAllowed()
	{
		var info = new DeviceInfo(0x17CC, 0x1700, null, null);
		info.FirmwareVersion.Should().BeNull();
		info.SerialNumber.Should().BeNull();
	}

	[Fact]
	public void EqualRecords_AreEqual()
		=> new DeviceInfo(1, 2, "a", "b").Should().Be(new DeviceInfo(1, 2, "a", "b"));

	[Fact]
	public void DifferentRecords_AreNotEqual()
		=> new DeviceInfo(1, 2, "a", "b").Should().NotBe(new DeviceInfo(1, 2, "a", "c"));
}
