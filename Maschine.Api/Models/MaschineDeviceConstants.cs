namespace Maschine.Api.Models;

/// <summary>
/// Known USB VID/PID constants for Native Instruments Maschine controllers.
/// </summary>
public static class MaschineDeviceConstants
{
	/// <summary>USB Vendor ID for Native Instruments.</summary>
	public const int VendorId = 0x17CC;

	/// <summary>USB Product ID for the Maschine Mikro MK3.</summary>
	public const int MikroMk3ProductId = 0x1700;

	/// <summary>Number of pressure-sensitive pads on the Mikro MK3.</summary>
	public const int MikroMk3PadCount = 16;

	/// <summary>Number of assignable buttons on the Mikro MK3.</summary>
	public const int MikroMk3ButtonCount = 45;

	/// <summary>Number of rotary encoders on the Mikro MK3.</summary>
	public const int MikroMk3EncoderCount = 9;

	/// <summary>
	/// Number of physical touch-strip LEDs on the Mikro MK3.
	/// These occupy the first 25 of the 35 strip slots in the unified light packet
	/// (light IDs 55–79); slots 80–89 are unused padding.
	/// </summary>
	public const int MikroMk3TouchStripLedCount = 25;
}
