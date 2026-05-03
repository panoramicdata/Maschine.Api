using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Manages encoder events for the Maschine Mikro MK3.
/// </summary>
internal sealed class MaschineEncoders : IEncoders
{
	private const int TouchStripEncoderIndex = 8;
	private byte? _lastTouchStripAbsolute;
	private byte? _lastTouchStripButtonAbsolute;

	/// <inheritdoc/>
	public event EventHandler<EncoderDelta>? EncoderChanged;

	/// <summary>
	/// Applies a 0x02 input report to the touch-strip decoder.
	/// </summary>
	/// <remarks>
	/// Empirical trace behavior for the touch strip:
	///   report[0] = 0x02
	///   report[1] = 0x00
	///   report[2] = absolute coarse position (commonly 0x40..0x4F while active)
	/// The report format for rotary encoders remains unknown; this currently emits only
	/// touch-strip deltas on encoder index 8.
	/// </remarks>
	internal void ApplyTouchStripReport(byte[] report)
	{
		if (report.Length < 3 || report[0] != MikroMk3Protocol.PadPressureReportId)
		{
			return;
		}

		// Current calibrated traces identify touch-strip movement under index 0x00.
		if (report[1] != 0x00)
		{
			return;
		}

		var current = report[2];
		if (!_lastTouchStripAbsolute.HasValue)
		{
			_lastTouchStripAbsolute = current;
			return;
		}

		var previous = _lastTouchStripAbsolute.Value;
		if (current == previous)
		{
			return;
		}

		var delta = current - previous;
		if (delta > 127)
		{
			delta -= 256;
		}
		else if (delta < -127)
		{
			delta += 256;
		}

		_lastTouchStripAbsolute = current;

		// Scale to keep existing demo thresholds responsive.
		EncoderChanged?.Invoke(this, new EncoderDelta(TouchStripEncoderIndex, delta * 16));
	}

	/// <summary>
	/// Applies a 0x01 button report to the touch-strip decoder.
	/// </summary>
	/// <remarks>
	/// Fresh hardware traces show slider movement in button reports under bytes 8/9:
	///   report[0] = 0x01
	///   report[10] = coarse slider absolute (monotonic across movement)
	/// Bytes 8/9 also vary, but byte 10 gives the most stable axis for real-time
	/// direction and magnitude in the demo.
	/// This path is used for live slider behavior in the demo while pad pressure
	/// remains on 0x02.
	/// </remarks>
	internal void ApplyTouchStripButtonReport(byte[] report)
	{
		if (report.Length < 11 || report[0] != MikroMk3Protocol.ButtonReportId)
		{
			return;
		}

		var current = report[10];
		if (!_lastTouchStripButtonAbsolute.HasValue)
		{
			_lastTouchStripButtonAbsolute = current;
			return;
		}

		var previous = _lastTouchStripButtonAbsolute.Value;
		if (current == previous)
		{
			return;
		}

		var delta = current - previous;
		if (delta > 127)
		{
			delta -= 256;
		}
		else if (delta < -127)
		{
			delta += 256;
		}

		_lastTouchStripButtonAbsolute = current;
		EncoderChanged?.Invoke(this, new EncoderDelta(TouchStripEncoderIndex, delta));
	}
}
