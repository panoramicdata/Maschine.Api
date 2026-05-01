using System.Diagnostics.CodeAnalysis;
using HidSharp;

namespace Maschine.Api.Internal;

/// <summary>
/// HidSharp-backed factory for opening HID device connections.
/// Excluded from code coverage as it is a thin hardware-access wrapper.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class HidSharpDeviceFactory : IHidDeviceFactory
{
	/// <inheritdoc/>
	public IHidDevice? TryOpen(int vendorId, int productId, int deviceIndex)
	{
		var device = DeviceList.Local
			.GetHidDevices(vendorId, productId)
			.Skip(deviceIndex)
			.FirstOrDefault();

		if (device is null)
		{
			return null;
		}

		var stream = device.Open();
		stream.ReadTimeout = Timeout.Infinite;
		return new HidSharpDevice(stream, device.GetMaxOutputReportLength(), device.GetMaxFeatureReportLength());
	}
}
