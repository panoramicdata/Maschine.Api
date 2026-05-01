namespace Maschine.Api.Exceptions;

/// <summary>
/// Thrown when no Maschine device matching the configured VID/PID can be found.
/// </summary>
public sealed class MaschineDeviceNotFoundException : Exception
{
	/// <summary>Initialises a new instance with the VID and PID that were not found.</summary>
	/// <param name="vendorId">The USB Vendor ID that was searched for.</param>
	/// <param name="productId">The USB Product ID that was searched for.</param>
	public MaschineDeviceNotFoundException(int vendorId, int productId)
		: base($"No Maschine device found with VID 0x{vendorId:X4} / PID 0x{productId:X4}. " +
		       "Ensure the device is connected and drivers are installed.")
	{
		VendorId = vendorId;
		ProductId = productId;
	}

	/// <summary>The USB Vendor ID that was not found.</summary>
	public int VendorId { get; }

	/// <summary>The USB Product ID that was not found.</summary>
	public int ProductId { get; }
}
