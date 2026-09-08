using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Maschine.Api.Widgets;
using Microsoft.Extensions.Logging;

namespace Maschine.Demo;

/// <summary>
/// Surface output for <see cref="DemoController"/>: the guarded LED and dot-matrix writes that
/// tolerate a device disappearing mid-demo.
/// </summary>
internal sealed partial class DemoController
{
	private async Task TryInitializeSurfaceAsync(CancellationToken cancellationToken)
	{
		if (_pads is null || _buttons is null)
		{
			return;
		}

		try
		{
			if (_touchStrip is not null)
			{
				await _touchStrip.SetAllLedsAsync(0, cancellationToken).ConfigureAwait(false);
			}

			for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
			{
				await _pads.SetColorAsync(pad, GetPadBaseColor(pad), cancellationToken).ConfigureAwait(false);
			}
			_touchStripLevel = 13;
			_touchStripRenderedLevel = -1;
			if (_drumPlayer is not null)
			{
				_ = _drumPlayer.TryActivateMode(_activeInstrumentMode, cycleVariant: false, out var instrumentName, out var variantIndex, out var variantCount);
				_logger.LogInformation("Initial instrument mode: {Mode}, variant={Variant}/{VariantCount}, instrument={Instrument}", _activeInstrumentMode, variantIndex + 1, variantCount, instrumentName);
			}

			_drumPlayer?.SetVolumeFromStripLevel(_touchStripLevel);
			await UpdateTouchStripLedsCoalescedAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "LED write failed during startup.");
		}
	}

	private async Task TrySetAllLedsAsync(PadColor padColor, byte buttonBrightness, string phase, CancellationToken cancellationToken)
	{
		if (_pads is null || _buttons is null)
		{
			return;
		}

		try
		{
			try
			{
				await _buttons.SetAllLedsAsync(buttonBrightness, cancellationToken).ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				// Key-managed modes can disallow global button writes.
			}

			await _pads.SetAllColorsAsync(padColor, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "LED write failed during {Phase}.", phase);
		}
	}

	private async Task TrySetPadColorAsync(int padIndex, PadColor color)
	{
		if (_pads is null)
		{
			return;
		}

		try
		{
			await _pads.SetColorAsync(MapPadIndexWithVerticalFlip(padIndex), color).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Pad write failed for P{PadIndex}.", padIndex);
		}
	}

	private async Task TrySetButtonLedAsync(int buttonIndex, byte brightness)
	{
		if (_buttons is null)
		{
			return;
		}

		try
		{
			await _buttons.SetLedAsync(buttonIndex, brightness).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Button write failed for B{ButtonIndex}.", buttonIndex);
		}
	}

	private async Task TrySetPadColorSpaceAsync(CancellationToken cancellationToken)
	{
		if (_pads is null)
		{
			return;
		}

		try
		{
			for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
			{
				var color = GetPadBaseColor(pad);
				await _pads.SetColorAsync(pad, color, cancellationToken).ConfigureAwait(false);
			}

			_logger.LogInformation("Pad color-space written across all 16 pads:");
			for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
			{
				var color = GetPadBaseColor(pad);
				_logger.LogInformation("  P{Pad,2} -> {Color}", pad, FormatColor(color));
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Pad color-space write failed.");
		}
	}

	private async Task TrySetDotMatrixTestPatternAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.SetDotMatrixTestPatternAsync(cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Dot-matrix test pattern written.");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Dot-matrix write failed.");
		}
	}

	private async Task TrySetDotMatrixZebraAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.SetDotMatrixZebraLinesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Dot-matrix zebra pattern written.");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Dot-matrix zebra write failed.");
		}
	}
}
