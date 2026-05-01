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
internal const byte PadPressureReportId = 0x20;

/// <summary>HID input report ID carrying button state data.</summary>
internal const byte ButtonReportId = 0x01;

/// <summary>HID input report ID carrying encoder delta data.</summary>
internal const byte EncoderReportId = 0x02;

// ── Report sizes (bytes, including the leading report-ID byte) ──────────

/// <summary>Total byte length of a pad-pressure input report.</summary>
internal const int PadPressureReportLength = 33; // 1 ID + 16 pads × 2 bytes

/// <summary>Total byte length of a button input report.</summary>
internal const int ButtonReportLength = 6; // 1 ID + 5 bytes of bit-flags

/// <summary>Total byte length of an encoder input report.</summary>
internal const int EncoderReportLength = 10; // 1 ID + 9 encoder bytes

/// <summary>Total byte length of the pad-LED output report.</summary>
internal const int PadLedReportLength = 49; // 1 ID + 16 pads × 3 bytes (R, G, B)

/// <summary>
/// Total byte length of the button-LED output report.
/// Byte at offset <c>1 + buttonIndex</c> controls that button brightness (0 = off, 127 = max).
/// </summary>
internal const int ButtonLedReportLength = 80; // 1 ID + 79 brightness bytes (45 used, rest padding)

// ── Decoders ─────────────────────────────────────────────────────────────

/// <summary>
/// Parses a pad-pressure input report into a list of <see cref="PadState"/> values.
/// </summary>
/// <param name="report">Raw report bytes (must be at least <see cref="PadPressureReportLength"/> bytes).</param>
/// <returns>One <see cref="PadState"/> per pad (16 entries).</returns>
/// <exception cref="ArgumentException">Thrown when the report is too short or has an unexpected ID.</exception>
internal static IReadOnlyList<PadState> ParsePadPressureReport(byte[] report)
{
ValidateReport(report, PadPressureReportId, PadPressureReportLength);

var states = new PadState[MaschineDeviceConstants.MikroMk3PadCount];
for (var i = 0; i < MaschineDeviceConstants.MikroMk3PadCount; i++)
{
var offset = 1 + (i * 2);
var pressure = report[offset] | (report[offset + 1] << 8);
states[i] = new PadState(i, pressure & 0x0FFF); // 12-bit value
}

return states;
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
/// Parses an encoder input report into a list of <see cref="EncoderDelta"/> values.
/// Each non-zero byte represents a signed relative rotation for that encoder.
/// </summary>
/// <param name="report">Raw report bytes (must be at least <see cref="EncoderReportLength"/> bytes).</param>
/// <returns>Zero or more encoder deltas (only encoders that moved are included).</returns>
/// <exception cref="ArgumentException">Thrown when the report is too short or has an unexpected ID.</exception>
internal static IReadOnlyList<EncoderDelta> ParseEncoderReport(byte[] report)
{
ValidateReport(report, EncoderReportId, EncoderReportLength);

var deltas = new List<EncoderDelta>(MaschineDeviceConstants.MikroMk3EncoderCount);
for (var i = 0; i < MaschineDeviceConstants.MikroMk3EncoderCount; i++)
{
var raw = (sbyte)report[1 + i]; // signed byte: positive = CW, negative = CCW
if (raw != 0)
{
deltas.Add(new EncoderDelta(i, raw));
}
}

return deltas;
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
