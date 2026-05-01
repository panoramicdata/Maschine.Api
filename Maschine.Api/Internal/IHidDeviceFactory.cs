namespace Maschine.Api.Internal;

/// <summary>Creates HID device connections. Abstracted for testability.</summary>
internal interface IHidDeviceFactory
{
	/// <summary>
	/// Attempts to open a HID device by vendor/product ID.
	/// </summary>
	/// <param name="vendorId">USB Vendor ID.</param>
	/// <param name="productId">USB Product ID.</param>
	/// <param name="deviceIndex">Zero-based index when multiple matching devices are connected.</param>
	/// <returns>An open <see cref="IHidDevice"/>, or <see langword="null"/> if not found.</returns>
	IHidDevice? TryOpen(int vendorId, int productId, int deviceIndex);
}
