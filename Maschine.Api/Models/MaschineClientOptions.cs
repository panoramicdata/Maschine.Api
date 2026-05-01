namespace Maschine.Api.Models;

/// <summary>Options used to locate and connect to a Maschine device.</summary>
public sealed class MaschineClientOptions
{
	/// <summary>
	/// USB Vendor ID to match. Defaults to <see cref="MaschineDeviceConstants.VendorId"/>.
	/// </summary>
	public int VendorId { get; set; } = MaschineDeviceConstants.VendorId;

	/// <summary>
	/// USB Product ID to match. Defaults to <see cref="MaschineDeviceConstants.MikroMk3ProductId"/>.
	/// </summary>
	public int ProductId { get; set; } = MaschineDeviceConstants.MikroMk3ProductId;

	/// <summary>
	/// Zero-based index when multiple matching devices are connected. Defaults to 0.
	/// </summary>
	public int DeviceIndex { get; set; }
}
