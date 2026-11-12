using Microsoft.Extensions.Logging;

namespace Maschine.Api;

// LoggerMessage delegates (CA1848 compliance)
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static partial class MaschineClientLog
{
	[LoggerMessage(Level = LogLevel.Information, Message = "Connected to Maschine device VID=0x{VendorId:X4} PID=0x{ProductId:X4}.")]
	public static partial void Connected(ILogger logger, int vendorId, int productId);

	[LoggerMessage(Level = LogLevel.Information, Message = "Disconnected from Maschine device.")]
	public static partial void Disconnected(ILogger logger);

	[LoggerMessage(Level = LogLevel.Error, Message = "Error reading from Maschine device.")]
	public static partial void ReadError(ILogger logger, Exception ex);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Unhandled report ID 0x{ReportId:X2}.")]
	public static partial void UnhandledReport(ILogger logger, byte reportId);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring malformed report ID 0x{ReportId:X2} (len={Length}).")]
	public static partial void MalformedReport(ILogger logger, byte reportId, int length);

	[LoggerMessage(Level = LogLevel.Debug, Message = "{Message}")]
	public static partial void TraceLine(ILogger logger, string message);
}
