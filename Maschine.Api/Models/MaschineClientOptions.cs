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

	/// <summary>
	/// Forces output writes to use the unified 0x80 light packet path instead of legacy split reports.
	/// Enable this on devices where legacy reports are accepted but LEDs do not visibly update.
	/// </summary>
	public bool ForceUnifiedLightOutput { get; set; }
}
