using Maschine.Api.Models;
using System.Threading;

namespace Maschine.Api.Internal;

/// <summary>
/// Maintains and writes the unified Mikro MK3 light packet format.
/// Report: ID 0x80 + 90 bytes (39 buttons + 16 pads + 35 strip).
/// Each byte: bits 0-1 = intensity (0=low, 1=med, 2=high, 3=faded);
///            bits 2-7 = palette colour index (0=off, 1=red … 17=white).
/// </summary>
internal sealed class MikroMk3UnifiedLights : IDisposable
{
	private const int LightDataLength = 90;  // 39 buttons + 16 pads + 35 strip
	private const int ReportLength = 1 + LightDataLength;
	private const int FirstPadLightId = 39;
	private const int FirstStripLightId = 55;

	// Pad index (0-15) -> hardware light ID order used by Mikro MK3.
	private static readonly byte[] s_padIndexToLightId =
	[
		51, 52, 53, 54,
		47, 48, 49, 50,
		43, 44, 45, 46,
		39, 40, 41, 42,
	];

	private readonly IHidDevice _device;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly byte[] _report = new byte[ReportLength];
	private volatile bool _enabled;
	private bool _disposed;

	internal MikroMk3UnifiedLights(IHidDevice device)
	{
		_device = device;
		_report[0] = 0x80;
	}

	internal bool IsEnabled => _enabled;

	internal void Enable() => _enabled = true;

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_gate.Dispose();
	}

	internal Task SetButtonBrightnessAsync(int buttonIndex, byte brightness, CancellationToken cancellationToken)
	{
		if (buttonIndex < 0 || buttonIndex >= MaschineDeviceConstants.MikroMk3ButtonCount)
		{
			throw new ArgumentOutOfRangeException(nameof(buttonIndex), buttonIndex,
				$"Button index must be 0-{MaschineDeviceConstants.MikroMk3ButtonCount - 1}.");
		}

		// Only the first 39 button slots are directly addressable in this packet.
		if (buttonIndex >= FirstPadLightId)
		{
			return Task.CompletedTask;
		}

		var value = MikroMk3LightEncoding.ScaleButtonBrightness(brightness);
		return WriteIfChangedAsync(report => SetSlot(report, buttonIndex, value), cancellationToken);
	}

	internal Task SetAllButtonBrightnessAsync(byte brightness, CancellationToken cancellationToken)
	{
		var value = MikroMk3LightEncoding.ScaleButtonBrightness(brightness);
		return WriteIfChangedAsync(
			report => SetSlots(report, FirstPadLightId, _ => value, static i => i),
			cancellationToken);
	}

	internal Task SetPadColorAsync(int padIndex, PadColor color, CancellationToken cancellationToken)
	{
		if (padIndex < 0 || padIndex >= MaschineDeviceConstants.MikroMk3PadCount)
		{
			throw new ArgumentOutOfRangeException(nameof(padIndex), padIndex,
				$"Pad index must be 0-{MaschineDeviceConstants.MikroMk3PadCount - 1}.");
		}

		var value = MikroMk3LightEncoding.EncodePadColor(color);
		return WriteIfChangedAsync(
			report => SetSlot(report, s_padIndexToLightId[padIndex], value),
			cancellationToken);
	}

	internal Task SetAllPadColorsAsync(PadColor color, CancellationToken cancellationToken)
	{
		var value = MikroMk3LightEncoding.EncodePadColor(color);
		return WriteIfChangedAsync(
			report => SetSlots(report, s_padIndexToLightId.Length, _ => value, static i => s_padIndexToLightId[i]),
			cancellationToken);
	}

	internal Task SetStripLedAsync(int position, byte brightness, CancellationToken cancellationToken)
	{
		if (position < 0 || position >= MaschineDeviceConstants.MikroMk3TouchStripLedCount)
		{
			throw new ArgumentOutOfRangeException(nameof(position), position,
				$"Strip LED position must be 0–{MaschineDeviceConstants.MikroMk3TouchStripLedCount - 1}.");
		}

		var value = MikroMk3LightEncoding.ScaleButtonBrightness(brightness);
		return WriteIfChangedAsync(
			report => SetSlot(report, FirstStripLightId + position, value),
			cancellationToken);
	}

	internal Task SetAllStripLedsAsync(byte brightness, CancellationToken cancellationToken)
	{
		var value = MikroMk3LightEncoding.ScaleButtonBrightness(brightness);
		return WriteIfChangedAsync(
			report => SetSlots(report, MaschineDeviceConstants.MikroMk3TouchStripLedCount, _ => value, StripSlot),
			cancellationToken);
	}

	internal Task SetStripLedsAsync(IReadOnlyList<byte> brightnessValues, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(brightnessValues);
		if (brightnessValues.Count != MaschineDeviceConstants.MikroMk3TouchStripLedCount)
		{
			throw new ArgumentException(
				$"Expected {MaschineDeviceConstants.MikroMk3TouchStripLedCount} brightness values, got {brightnessValues.Count}.",
				nameof(brightnessValues));
		}

		return WriteIfChangedAsync(
			report => SetSlots(report, brightnessValues.Count, i => MikroMk3LightEncoding.ScaleButtonBrightness(brightnessValues[i]), StripSlot),
			cancellationToken);
	}

	internal Task SetStripLedsColorAsync(IReadOnlyList<PadColor> colors, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(colors);
		if (colors.Count != MaschineDeviceConstants.MikroMk3TouchStripLedCount)
		{
			throw new ArgumentException(
				$"Expected {MaschineDeviceConstants.MikroMk3TouchStripLedCount} color values, got {colors.Count}.",
				nameof(colors));
		}

		return WriteIfChangedAsync(
			report => SetSlots(report, colors.Count, i => MikroMk3LightEncoding.EncodePadColor(colors[i]), StripSlot),
			cancellationToken);
	}

	private static int StripSlot(int index) => FirstStripLightId + index;

	/// <summary>
	/// Applies <paramref name="mutate"/> to the light packet under the write gate and sends the
	/// packet only if it actually changed, so repeated writes of the same state stay off the wire.
	/// </summary>
	/// <param name="mutate">Returns whether it changed any byte of the packet.</param>
	/// <param name="cancellationToken">Cancels waiting for the gate and the device write.</param>
	private async Task WriteIfChangedAsync(Func<byte[], bool> mutate, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (!mutate(_report))
			{
				return;
			}

			await _device.WriteAsync(_report, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <summary>Writes one light slot, reporting whether it changed.</summary>
	private static bool SetSlot(byte[] report, int lightId, byte value)
	{
		var offset = 1 + lightId;
		if (report[offset] == value)
		{
			return false;
		}

		report[offset] = value;
		return true;
	}

	/// <summary>
	/// Writes <paramref name="count"/> light slots, reporting whether any of them changed.
	/// </summary>
	/// <param name="report">The light packet.</param>
	/// <param name="count">Number of logical positions to write.</param>
	/// <param name="valueAt">Value for the logical position.</param>
	/// <param name="lightIdAt">Hardware light ID for the logical position.</param>
	private static bool SetSlots(byte[] report, int count, Func<int, byte> valueAt, Func<int, int> lightIdAt)
	{
		var changed = false;
		for (var i = 0; i < count; i++)
		{
			changed |= SetSlot(report, lightIdAt(i), valueAt(i));
		}

		return changed;
	}
}
