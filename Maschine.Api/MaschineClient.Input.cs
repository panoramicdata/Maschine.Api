using Maschine.Api.Internal;

namespace Maschine.Api;

public sealed partial class MaschineClient
{
	private async Task RunReadLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				var report = await _device!.ReadAsync(cancellationToken).ConfigureAwait(false);
				if (report.Length == 0)
				{
					continue;
				}

				DispatchReport(report);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				MaschineClientLog.ReadError(_logger, ex);
				break;
			}
		}
	}

	[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	private void DispatchReport(byte[] report)
	{
		if (report.Length == 0)
		{
			return;
		}

		try
		{
			TraceInputReport(report);

			switch (report[0])
			{
				case MikroMk3Protocol.PadPressureReportId:
					_pads?.ApplyReport(report);
					break;

				case MikroMk3Protocol.ButtonReportId:
					_buttons?.ApplyReport(report);
					_encoders?.ApplyTouchStripButtonReport(report);
					break;

				default:
					MaschineClientLog.UnhandledReport(_logger, report[0]);
					break;
			}
		}
		catch (ArgumentException)
		{
			MaschineClientLog.MalformedReport(_logger, report[0], report.Length);
		}
	}
}
