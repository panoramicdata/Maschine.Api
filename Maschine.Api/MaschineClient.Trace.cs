using Maschine.Api.Internal;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Maschine.Api;

public sealed partial class MaschineClient
{
	private readonly Dictionary<byte, byte[]> _previousTracedReports = new();
	private readonly Dictionary<byte, DateTime> _previousTraceTimesById = new();
	private DateTime? _previousTraceTime;

	[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	private void TraceInputReport(byte[] report)
	{
		if (!_options.TraceInputReports || !_logger.IsEnabled(LogLevel.Debug))
		{
			return;
		}

		var kind = report[0] switch
		{
			MikroMk3Protocol.PadPressureReportId => "PAD",
			MikroMk3Protocol.ButtonReportId => "BUTTON",
			_ => "UNKNOWN",
		};
		var now = DateTime.UtcNow;
		var timestamp = now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
		var dtAll = _previousTraceTime.HasValue
			? $"dtAll={((now - _previousTraceTime.Value).TotalMilliseconds):0.0}ms"
			: "dtAll=init";
		var dtId = _previousTraceTimesById.TryGetValue(report[0], out var previousForId)
			? $"dtId={((now - previousForId).TotalMilliseconds):0.0}ms"
			: "dtId=init";
		_previousTraceTime = now;
		_previousTraceTimesById[report[0]] = now;

		var hex = BitConverter.ToString(report).Replace('-', ' ');
		var diff = BuildTraceDiff(report);
		var head = report.Length >= 4
			? $"head=[{report[1]:X2} {report[2]:X2} {report[3]:X2}] "
			: string.Empty;
#pragma warning disable CA1873
		MaschineClientLog.TraceLine(_logger, $"[{timestamp}Z] Input {kind} ID=0x{report[0]:X2} len={report.Length} {dtAll} {dtId} {diff} {head}bytes=[{hex}]");
#pragma warning restore CA1873
	}

	private string BuildTraceDiff(byte[] report)
	{
		if (!_previousTracedReports.TryGetValue(report[0], out var previous))
		{
			_previousTracedReports[report[0]] = (byte[])report.Clone();
			return "chg=init";
		}

		var max = Math.Min(previous.Length, report.Length);
		var changes = new List<string>();
		for (var i = 0; i < max; i++)
		{
			if (previous[i] != report[i])
			{
				changes.Add($"{i}:{previous[i]:X2}->{report[i]:X2}");
				if (changes.Count >= 10)
				{
					break;
				}
			}
		}

		if (previous.Length != report.Length)
		{
			changes.Add($"len:{previous.Length}->{report.Length}");
		}

		_previousTracedReports[report[0]] = (byte[])report.Clone();
		return changes.Count == 0 ? "chg=none" : $"chg=[{string.Join(',', changes)}]";
	}
}
