using System.Diagnostics.CodeAnalysis;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using HidSharp;

namespace Maschine.Api;

/// <summary>
/// Enumerates connected Maschine controllers using HidSharp.
/// Excluded from code coverage as it is a thin hardware-access wrapper.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class HidDeviceEnumerator : IDeviceEnumerator
{
	/// <inheritdoc/>
	public IReadOnlyList<DeviceInfo> Enumerate(int vendorId, int productId)
	{
		return DeviceList.Local
			.GetHidDevices(vendorId, productId)
			.Select(d => new DeviceInfo(d.VendorID, d.ProductID, null, d.GetFriendlyName()))
			.ToList();
	}
}
