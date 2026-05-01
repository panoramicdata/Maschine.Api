namespace Maschine.Api.Test;

public sealed class MaschineDeviceNotFoundExceptionTests
{
	[Fact]
	public void Properties_AreStoredCorrectly()
	{
		var ex = new MaschineDeviceNotFoundException(0x17CC, 0x1700);
		ex.VendorId.Should().Be(0x17CC);
		ex.ProductId.Should().Be(0x1700);
	}

	[Fact]
	public void Message_ContainsVidAndPid()
	{
		var ex = new MaschineDeviceNotFoundException(0x17CC, 0x1700);
		ex.Message.Should().Contain("17CC");
		ex.Message.Should().Contain("1700");
	}
}
