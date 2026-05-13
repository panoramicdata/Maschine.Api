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

	/// <summary>
	/// Global LED brightness scalar applied to pad and button writes.
	/// Range: 0-100, where 100 is full brightness.
	/// </summary>
	public int GlobalLedBrightnessPercent { get; set; } = 100;

	/// <summary>
	/// Emit raw HID input report diagnostics through the client logger.
	/// Useful when reverse-engineering report IDs/lengths on a live device.
	/// </summary>
	public bool TraceInputReports { get; set; }

	/// <summary>
	/// Per-key mode map for LED-backed keys.
	/// The library default uses latch behavior for most keys, with specific overrides.
	/// </summary>
	public Dictionary<MikroMk3Button, KeyMode> KeyModes { get; set; } = KeyModeDefaults.Create();

	/// <summary>
	/// Optional key radio button groups applied by the key-mode engine.
	/// </summary>
	public List<RadioButtonGroupOptions> KeyRadioButtonGroups { get; set; } = [];

	/// <summary>
	/// Global fire-mode LED flash duration in milliseconds.
	/// </summary>
	public int KeyFireFlashDurationMs { get; set; } = 200;

	/// <summary>
	/// Optional per-key overrides for fire-mode flash duration in milliseconds.
	/// Set an entry to null to use <see cref="KeyFireFlashDurationMs"/>.
	/// </summary>
	public Dictionary<MikroMk3Button, int?> KeyFireFlashDurationOverridesMs { get; set; } = [];

	/// <summary>
	/// When true, external LED writes are permitted even while keys are in a managed
	/// mode (e.g. <see cref="KeyMode.LatchEarly"/>). The write is applied to the
	/// hardware and the managed on/off state is updated to remain consistent, so
	/// subsequent latch transitions continue to behave correctly.
	/// </summary>
	public bool AllowExternalLedOverrides { get; set; }
}
