using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Microsoft.Extensions.Logging;

namespace Maschine.Demo;

/// <summary>
/// Touch-strip handling for <see cref="DemoController"/>: the slider level, drum volume and the
/// coalesced LED updates.
/// </summary>
internal sealed partial class DemoController
{
	private int _touchStripRenderedLevel = -1;

	private void OnEncoderChanged(EncoderDelta delta)
	{
		const int TouchStripNoiseFloor = 20;
		const int EncoderNoiseFloor = 24;
		const int LogThrottleMs = 60;

		var noiseFloor = delta.Index == TouchStripEncoderIndex ? TouchStripNoiseFloor : EncoderNoiseFloor;
		if (Math.Abs(delta.Delta) < noiseFloor)
		{
			return;
		}

		var step = Math.Sign(delta.Delta);
		if (step == 0)
		{
			return;
		}

		if (delta.Index == TouchStripEncoderIndex)
		{
			// Calibrated axis for strip LEDs.
			var nowUtc = DateTime.UtcNow;
			bool shouldLog;
			int level;
			lock (_animationSync)
			{
				shouldLog = (nowUtc - _lastEncoderLogUtc[delta.Index]).TotalMilliseconds >= LogThrottleMs;
				if (shouldLog)
				{
					_lastEncoderLogUtc[delta.Index] = nowUtc;
				}

				_touchStripLevel = Math.Clamp(_touchStripLevel + step, 0, MaschineDeviceConstants.MikroMk3TouchStripLedCount);
				level = _touchStripLevel;
			}

			_drumPlayer?.SetVolumeFromStripLevel(level);

			if (shouldLog)
			{
				_logger.LogInformation("Slider at position {Position} (drum volume {VolumePercent}%)", level, (int)Math.Round((level / 25.0) * 100.0));
				_logger.LogDebug("Slider delta {Delta:+#;-#;0} (encoder index {Index})", delta.Delta, delta.Index);
			}

			_ = UpdateTouchStripLedsCoalescedAsync();
		}
		else
		{
			// Ignore auxiliary encoder noise in the demo; it obscures pad/button logs.
		}
	}

	private async Task UpdateTouchStripLedsCoalescedAsync()
	{
		if (_touchStrip is null)
		{
			return;
		}

		if (!await _touchStripUpdateGate.WaitAsync(0).ConfigureAwait(false))
		{
			return;
		}

		try
		{
			// Re-check after each write: the level can move again while the HID write is in flight,
			// and this loop is the only writer, so it must drain to the latest value before exiting.
			while (ReadTouchStripLevel() != _touchStripRenderedLevel)
			{
				var level = ReadTouchStripLevel();
				await _touchStrip.SetLedsAsync(BuildTouchStripLeds(level)).ConfigureAwait(false);
				_touchStripRenderedLevel = level;
			}
		}
		finally
		{
			_touchStripUpdateGate.Release();
		}
	}

	private int ReadTouchStripLevel()
	{
		lock (_animationSync)
		{
			return _touchStripLevel;
		}
	}

	private static PadColor[] BuildTouchStripLeds(int level)
	{
		var leds = new PadColor[MaschineDeviceConstants.MikroMk3TouchStripLedCount];
		var activeColor = level == 0 ? PadColor.Off : s_touchStripDemoColors[level - 1];
		for (var i = 0; i < leds.Length; i++)
		{
			leds[i] = i < level ? activeColor : PadColor.Off;
		}

		return leds;
	}
}
