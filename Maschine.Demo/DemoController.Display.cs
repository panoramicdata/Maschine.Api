using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Maschine.Api.Widgets;
using Microsoft.Extensions.Logging;

namespace Maschine.Demo;

/// <summary>
/// The dot-matrix showcase for <see cref="DemoController"/>: the animation loop, the dashboards
/// it cycles through, and the remaining one-shot LED routines and pad-mapping helpers.
/// </summary>
internal sealed partial class DemoController
{
	private async Task RunDotMatrixShowcaseAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Dashboard demo started. Knob position selects the active Dashboard.");
		byte[]? previousFrame = null;
		while (!cancellationToken.IsCancellationRequested)
		{
			DotMatrixDashboard dashboard;
			var invert = false;
			var frameNumber = 0;
			lock (_animationSync)
			{
				dashboard = _dashboards[_selectedDashboardIndex];
				invert = _isDashboardInverted;
				frameNumber = _audioFrame++;
			}

			DemoFakeAudio.Update(dashboard, frameNumber);

			var frame = dashboard.BuildBitmap();
			if (invert)
			{
				InvertBitmap(frame);
			}

			if (previousFrame is null || !frame.AsSpan().SequenceEqual(previousFrame))
			{
				await _client.SetDotMatrixBitmapAsync(frame, cancellationToken: cancellationToken).ConfigureAwait(false);
				previousFrame = frame;
			}

			var signedVelocity = GetDisplayVelocity();
			dashboard.AdvanceFrame(Math.Sign(signedVelocity));

			var velocity = Math.Abs(signedVelocity);
			var delayMs = velocity == 0 ? 100 : s_zebraSpeedMs[Math.Clamp(velocity, 1, s_zebraSpeedMs.Length) - 1];
			await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
		}
	}

	private int GetDisplayVelocity() => _displayVelocity;

	private async Task TryClearDotMatrixAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.ClearDotMatrixAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			// Best-effort cleanup.
		}
	}

	private async Task RunLedSelfTestAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Running LED self-test: global colors, pad chase, button chase.");

		var globalColors = new[]
		{
			PadColor.Red,
			PadColor.Green,
			PadColor.Blue,
			PadColor.White,
		};

		foreach (var color in globalColors)
		{
			await TrySetAllLedsAsync(color, 127, "self-test-global", cancellationToken).ConfigureAwait(false);
			await Task.Delay(220, cancellationToken).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-reset", cancellationToken).ConfigureAwait(false);

		for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
		{
			await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-pad-reset", cancellationToken).ConfigureAwait(false);
			await TrySetPadColorAsync(pad, GetPadBaseColor(pad)).ConfigureAwait(false);
			_logger.LogInformation("Self-test pad chase: P{Pad,2}", pad);
			await Task.Delay(120, cancellationToken).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-before-buttons", cancellationToken).ConfigureAwait(false);

		for (var button = 0; button < MaschineDeviceConstants.MikroMk3ButtonCount; button++)
		{
			await TrySetButtonLedAsync(button, 127).ConfigureAwait(false);
			_logger.LogInformation("Self-test button chase: B{Button,2}", button);
			await Task.Delay(80, cancellationToken).ConfigureAwait(false);
			await TrySetButtonLedAsync(button, 0).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-complete", cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("LED self-test complete. Interactive mode continues.");
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private static PadColor GetPadBaseColor(int rawPadIndex)
	{
		var mappedIndex = MapPadIndexWithVerticalFlip(rawPadIndex);
		return s_padColors[mappedIndex];
	}

	private static int MapPadIndexWithVerticalFlip(int rawPadIndex)
	{
		var row = rawPadIndex / 4;
		var col = rawPadIndex % 4;
		var flippedRow = 3 - row;
		return (flippedRow * 4) + col;
	}

	private static int ToUserPadNumber(int rawPadIndex)
		=> MapPadIndexWithVerticalFlip(rawPadIndex) + 1;

	private static string FormatColor(PadColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

	private static void InvertBitmap(byte[] bitmap)
	{
		for (var i = 0; i < bitmap.Length; i++)
		{
			bitmap[i] = (byte)~bitmap[i];
		}
	}

	private void PrintMappings()
	{
		_logger.LogInformation("=== Maschine Mikro MK3 Reactive Demo ===");
		_logger.LogInformation("Behavior:");
		_logger.LogInformation("  PAD MODE / KEYBOARD / CHORDS -> API radio group (one lit), press active mode again to cycle 3 sounds");
		_logger.LogInformation("  Button LEDs are managed by API key modes (except explicit EventsOnly keys)");
		_logger.LogInformation("  Pad press     -> flash white and trigger a velocity-aware drum hit");
		_logger.LogInformation("  Knob          -> selects the active Dashboard by absolute position");
		_logger.LogInformation("  Logo button   -> toggles Dashboard invert mode");
		_logger.LogInformation("  Slider        -> updates strip LEDs and controls demo drum volume");
		_logger.LogInformation("  Dashboard     -> whole display made of non-overlapping Widgets");
		_logger.LogInformation("  Demo pages     -> {DashboardCount} Dashboards available", _dashboards.Length);
	}
}
