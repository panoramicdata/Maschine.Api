using Maschine.Api.Models;

namespace Maschine.Api.Interfaces;

/// <summary>
/// Provides enumeration of connected Maschine devices.
/// </summary>
public interface IDeviceEnumerator
{
	/// <summary>
	/// Returns all connected Maschine devices matching the given VID and PID.
	/// </summary>
	/// <param name="vendorId">USB Vendor ID.</param>
	/// <param name="productId">USB Product ID.</param>
	IReadOnlyList<DeviceInfo> Enumerate(int vendorId, int productId);
}
