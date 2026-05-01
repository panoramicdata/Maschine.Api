namespace Maschine.Api.Models;

/// <summary>
/// Identifies a detected Maschine HID device.
/// </summary>
/// <param name="VendorId">USB Vendor ID.</param>
/// <param name="ProductId">USB Product ID.</param>
/// <param name="FirmwareVersion">Firmware version string, or <see langword="null"/> if not available.</param>
/// <param name="SerialNumber">Device serial number, or <see langword="null"/> if not available.</param>
public sealed record DeviceInfo(int VendorId, int ProductId, string? FirmwareVersion, string? SerialNumber);
