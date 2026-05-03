using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Controls the touch-strip LEDs on the Maschine Mikro MK3 via the unified light packet.
/// </summary>
internal sealed class MaschineTouchStrip : ITouchStrip
{
	private readonly MikroMk3UnifiedLights _unifiedLights;
	private readonly LedBrightnessController _brightness;

	internal MaschineTouchStrip(MikroMk3UnifiedLights unifiedLights, LedBrightnessController brightness)
	{
		_unifiedLights = unifiedLights;
		_brightness = brightness;
	}

	/// <inheritdoc/>
	public Task SetLedAsync(int position, byte brightness, CancellationToken cancellationToken = default)
	{
		var scaled = _brightness.Scale(brightness);
		return _unifiedLights.SetStripLedAsync(position, scaled, cancellationToken);
	}

	/// <inheritdoc/>
	public Task SetAllLedsAsync(byte brightness, CancellationToken cancellationToken = default)
	{
		var scaled = _brightness.Scale(brightness);
		return _unifiedLights.SetAllStripLedsAsync(scaled, cancellationToken);
	}

	/// <inheritdoc/>
	public Task SetLedsAsync(IReadOnlyList<byte> brightnessValues, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(brightnessValues);
		if (brightnessValues.Count != MaschineDeviceConstants.MikroMk3TouchStripLedCount)
		{
			throw new ArgumentException(
				$"Expected {MaschineDeviceConstants.MikroMk3TouchStripLedCount} brightness values, got {brightnessValues.Count}.",
				nameof(brightnessValues));
		}

		var scaled = new byte[MaschineDeviceConstants.MikroMk3TouchStripLedCount];
		for (var i = 0; i < scaled.Length; i++)
		{
			scaled[i] = _brightness.Scale(brightnessValues[i]);
		}

		return _unifiedLights.SetStripLedsAsync(scaled, cancellationToken);
	}

	/// <inheritdoc/>
	public Task SetLedsAsync(IReadOnlyList<PadColor> colors, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(colors);
		if (colors.Count != MaschineDeviceConstants.MikroMk3TouchStripLedCount)
		{
			throw new ArgumentException(
				$"Expected {MaschineDeviceConstants.MikroMk3TouchStripLedCount} color values, got {colors.Count}.",
				nameof(colors));
		}

		return _unifiedLights.SetStripLedsColorAsync(colors, cancellationToken);
	}
}
