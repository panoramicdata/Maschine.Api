using Maschine.Api.Models;

namespace Maschine.Api.Internal;

/// <summary>
/// Pure-function HID report encoder/decoder for the Maschine Mikro MK3.
/// </summary>
/// <remarks>
/// All methods are static and side-effect free so they can be fully unit-tested
/// without hardware. Byte offsets are based on community reverse-engineering of
/// the MK3 HID protocol (VID 0x17CC / PID 0x1700) and should be verified against
/// a live device if the protocol is ever revised.
/// </remarks>
internal static class MikroMk3Protocol
{
// ── Output report IDs ────────────────────────────────────────────────────

/// <summary>HID output report ID used to set pad LED colours.</summary>
internal const byte PadLedReportId = 0x80;

/// <summary>HID output report ID used to set button LED brightness.</summary>
/// <remarks>Based on community reverse-engineering; verify with hardware if LEDs do not respond.</remarks>
internal const byte ButtonLedReportId = 0x81;

// ── Input report IDs ─────────────────────────────────────────────────────

/// <summary>HID input report ID carrying pad pressure data.</summary>
	internal const byte PadPressureReportId = 0x02;

	/// <summary>HID input report ID carrying button state data.</summary>
	internal const byte ButtonReportId = 0x01;
// ── Report sizes (bytes, including the leading report-ID byte) ──────────

/// <summary>Total byte length of a pad-pressure input report.</summary>
	internal const int PadPressureReportLength = 64; // 1 ID + pad index + pressure + noise + 61 reserved bytes

	/// <summary>Total byte length of a button input report.</summary>
	/// <remarks>
	/// The full report is 14 bytes: 1 ID + 5 button bytes + 1 encoder-touch byte + 1 encoder-value byte + 6 strip/reserved bytes.
	/// </remarks>
	internal const int ButtonReportLength = 14; // 1 ID + 13 data bytes
/// <summary>Total byte length of the pad-LED output report.</summary>
internal const int PadLedReportLength = 49; // 1 ID + 16 pads × 3 bytes (R, G, B)

/// <summary>
/// Total byte length of the button-LED output report.
/// Byte at offset <c>1 + buttonIndex</c> controls that button brightness (0 = off, 127 = max).
/// </summary>
internal const int ButtonLedReportLength = 80; // 1 ID + 79 brightness bytes (45 used, rest padding)

// ── Decoders ─────────────────────────────────────────────────────────────

/// <summary>
/// Parses a pad-pressure input report into a <see cref="PadState"/> for the active pad.
/// </summary>
/// <remarks>
/// Hardware protocol (confirmed from calibration traces):
///   report[0] = 0x02 (report ID)
///   report[1] = pad index (0x00–0x0F)
///   report[2] = pressure coarse: 0x40 = idle/rest, &gt;0x40 = active (pressure = raw - 0x40),
///               0x30 = released/lifted transition (treated as pressure 0)
///   report[3] = sub-byte noise (ignored)
/// Pressure is scaled by 256 so values span the full 0–3840 range (compatible with
/// 12-bit consumers; max raw delta of 15 × 256 = 3840).
/// Physical pad layout (pad 1 = bottom-left, pad 16 = top-right):
///   Bottom row:  indices 12, 13, 14, 15
///   Row 2:       indices  8,  9, 10, 11
///   Row 3:       indices  4,  5,  6,  7
///   Top row:     indices  0,  1,  2,  3
/// </remarks>
/// <param name="report">Raw report bytes (must be at least <see cref="PadPressureReportLength"/> bytes).</param>
/// <returns>A <see cref="PadState"/> for the pad that generated this report.</returns>
/// <exception cref="ArgumentException">Thrown when the report is too short or has an unexpected ID.</exception>
internal static PadState ParsePadPressureReport(byte[] report)
{
	ValidateReport(report, PadPressureReportId, PadPressureReportLength);

	var padIndex = report[1];
	var raw = report[2];
	var pressure = raw <= 0x40 ? 0 : (raw - 0x40) * 256;
	return new PadState(padIndex, pressure);
}

/// <summary>
/// Parses a button input report into a list of <see cref="ButtonState"/> values.
/// </summary>
/// <param name="report">Raw report bytes (must be at least <see cref="ButtonReportLength"/> bytes).</param>
/// <returns>One <see cref="ButtonState"/> per button (45 entries).</returns>
/// <exception cref="ArgumentException">Thrown when the report is too short or has an unexpected ID.</exception>
internal static IReadOnlyList<ButtonState> ParseButtonReport(byte[] report)
{
ValidateReport(report, ButtonReportId, ButtonReportLength);

var states = new ButtonState[MaschineDeviceConstants.MikroMk3ButtonCount];
for (var i = 0; i < MaschineDeviceConstants.MikroMk3ButtonCount; i++)
{
var byteIndex = 1 + (i / 8);
var bitIndex = i % 8;
var isPressed = byteIndex < report.Length && ((report[byteIndex] >> bitIndex) & 1) == 1;
states[i] = new ButtonState(i, isPressed);
}

return states;
}



/// <summary>
	/// Parses the encoder touch state from a button input report.
	/// </summary>
	/// <remarks>
	/// Byte layout from the mk3.bitproto:
	///   byte 6 bit 0  = encoder_touched (bool)
	///   byte 7 bits 0-3 = encoder_value (uint4, absolute knob position 0–15)
	/// </remarks>
	/// <param name="report">Raw report bytes (must be at least <see cref="ButtonReportLength"/> bytes).</param>
	/// <returns>An <see cref="EncoderTouchState"/> with touch flag and absolute knob value.</returns>
	internal static EncoderTouchState ParseEncoderTouchFromButtonReport(byte[] report)
	{
		ValidateReport(report, ButtonReportId, ButtonReportLength);
		var isTouched = (report[6] & 0x01) != 0;
		var knobValue = (byte)(report[7] & 0x0F);
		return new EncoderTouchState(isTouched, knobValue);
	}

	// ── Encoders ─────────────────────────────────────────────────────────────

/// <summary>
/// Builds a pad-LED output report that sets a single pad to the given colour.
/// </summary>
/// <param name="padIndex">Zero-based pad index (0-15).</param>
/// <param name="color">Target RGB colour.</param>
/// <returns>A <see cref="PadLedReportLength"/>-byte output report.</returns>
/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="padIndex"/> is out of range.</exception>
internal static byte[] BuildSinglePadColorReport(int padIndex, PadColor color)
{
if (padIndex < 0 || padIndex >= MaschineDeviceConstants.MikroMk3PadCount)
{
throw new ArgumentOutOfRangeException(nameof(padIndex), padIndex,
$"Pad index must be 0-{MaschineDeviceConstants.MikroMk3PadCount - 1}.");
}

var report = new byte[PadLedReportLength];
report[0] = PadLedReportId;
var offset = 1 + (padIndex * 3);
report[offset] = color.R;
report[offset + 1] = color.G;
report[offset + 2] = color.B;
return report;
}

/// <summary>
/// Builds a pad-LED output report that sets all pads to the same colour.
/// </summary>
/// <param name="color">Target RGB colour applied to every pad.</param>
/// <returns>A <see cref="PadLedReportLength"/>-byte output report.</returns>
internal static byte[] BuildAllPadsColorReport(PadColor color)
{
var report = new byte[PadLedReportLength];
report[0] = PadLedReportId;
for (var i = 0; i < MaschineDeviceConstants.MikroMk3PadCount; i++)
{
var offset = 1 + (i * 3);
report[offset] = color.R;
report[offset + 1] = color.G;
report[offset + 2] = color.B;
}

return report;
}

/// <summary>
/// Builds a pad-LED output report from an explicit per-pad color frame.
/// </summary>
/// <param name="colors">Exactly 16 colors, one for each pad index.</param>
/// <returns>A <see cref="PadLedReportLength"/>-byte output report.</returns>
/// <exception cref="ArgumentException">Thrown when color count does not match pad count.</exception>
internal static byte[] BuildPadColorsReport(IReadOnlyList<PadColor> colors)
{
ArgumentNullException.ThrowIfNull(colors);
if (colors.Count != MaschineDeviceConstants.MikroMk3PadCount)
{
throw new ArgumentException(
$"Expected {MaschineDeviceConstants.MikroMk3PadCount} pad colors, got {colors.Count}.",
nameof(colors));
}

var report = new byte[PadLedReportLength];
report[0] = PadLedReportId;
for (var i = 0; i < MaschineDeviceConstants.MikroMk3PadCount; i++)
{
var color = colors[i];
var offset = 1 + (i * 3);
report[offset] = color.R;
report[offset + 1] = color.G;
report[offset + 2] = color.B;
}

return report;
}

/// <summary>
/// Builds a button-LED output report that sets a single button brightness.
/// </summary>
/// <param name="buttonIndex">Zero-based button index (0-<see cref="MaschineDeviceConstants.MikroMk3ButtonCount"/> minus 1).</param>
/// <param name="brightness">Brightness level (0 = off, 127 = maximum).</param>
/// <returns>A <see cref="ButtonLedReportLength"/>-byte output report.</returns>
/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="buttonIndex"/> is out of range.</exception>
internal static byte[] BuildButtonLedReport(int buttonIndex, byte brightness)
{
if (buttonIndex < 0 || buttonIndex >= MaschineDeviceConstants.MikroMk3ButtonCount)
{
throw new ArgumentOutOfRangeException(nameof(buttonIndex), buttonIndex,
$"Button index must be 0-{MaschineDeviceConstants.MikroMk3ButtonCount - 1}.");
}

var report = new byte[ButtonLedReportLength];
report[0] = ButtonLedReportId;
report[1 + buttonIndex] = brightness;
return report;
}

/// <summary>
/// Builds a button-LED output report that sets all button LEDs to the same brightness.
/// </summary>
/// <param name="brightness">Brightness level (0 = off, 127 = maximum).</param>
/// <returns>A <see cref="ButtonLedReportLength"/>-byte output report.</returns>
internal static byte[] BuildAllButtonLedsReport(byte brightness)
{
var report = new byte[ButtonLedReportLength];
report[0] = ButtonLedReportId;
for (var i = 0; i < MaschineDeviceConstants.MikroMk3ButtonCount; i++)
{
report[1 + i] = brightness;
}

return report;
}

// ── Helpers ─────────────────────────────────────────────────────────────

private static void ValidateReport(byte[] report, byte expectedId, int minLength)
{
ArgumentNullException.ThrowIfNull(report);
if (report.Length < minLength)
{
throw new ArgumentException(
$"Report too short: expected at least {minLength} bytes, got {report.Length}.",
nameof(report));
}

if (report[0] != expectedId)
{
throw new ArgumentException(
$"Unexpected report ID 0x{report[0]:X2}: expected 0x{expectedId:X2}.",
nameof(report));
}
}
}
