using Maschine.Api.Models;
using System.Threading;

namespace Maschine.Api.Internal;

/// <summary>
/// Maintains and writes the unified Mikro MK3 light packet format:
/// report ID 0x80 + 80 bytes of light data.
/// </summary>
internal sealed class MikroMk3UnifiedLights : IDisposable
{
	private const int LightDataLength = 80;
	private const int ReportLength = 1 + LightDataLength;
	private const int FirstPadLightId = 39;

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

	internal async Task SetButtonBrightnessAsync(int buttonIndex, byte brightness, CancellationToken cancellationToken)
	{
		if (buttonIndex < 0 || buttonIndex >= MaschineDeviceConstants.MikroMk3ButtonCount)
		{
			throw new ArgumentOutOfRangeException(nameof(buttonIndex), buttonIndex,
				$"Button index must be 0-{MaschineDeviceConstants.MikroMk3ButtonCount - 1}.");
		}

		// Only the first 39 button slots are directly addressable in this packet.
		if (buttonIndex >= FirstPadLightId)
		{
			return;
		}

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var value = ScaleButtonBrightness(brightness);
			if (_report[1 + buttonIndex] == value)
			{
				return;
			}

			_report[1 + buttonIndex] = value;
			await _device.WriteAsync(_report, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	internal async Task SetAllButtonBrightnessAsync(byte brightness, CancellationToken cancellationToken)
	{
		var value = ScaleButtonBrightness(brightness);

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var changed = false;
			for (var i = 0; i < FirstPadLightId; i++)
			{
				var offset = 1 + i;
				if (_report[offset] != value)
				{
					_report[offset] = value;
					changed = true;
				}
			}

			if (!changed)
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

	internal async Task SetPadColorAsync(int padIndex, PadColor color, CancellationToken cancellationToken)
	{
		if (padIndex < 0 || padIndex >= MaschineDeviceConstants.MikroMk3PadCount)
		{
			throw new ArgumentOutOfRangeException(nameof(padIndex), padIndex,
				$"Pad index must be 0-{MaschineDeviceConstants.MikroMk3PadCount - 1}.");
		}

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var lightId = s_padIndexToLightId[padIndex];
			var value = EncodePadColor(color);
			var offset = 1 + lightId;
			if (_report[offset] == value)
			{
				return;
			}

			_report[offset] = value;
			await _device.WriteAsync(_report, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	internal async Task SetAllPadColorsAsync(PadColor color, CancellationToken cancellationToken)
	{
		var value = EncodePadColor(color);

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var changed = false;
			for (var i = 0; i < s_padIndexToLightId.Length; i++)
			{
				var offset = 1 + s_padIndexToLightId[i];
				if (_report[offset] != value)
				{
					_report[offset] = value;
					changed = true;
				}
			}

			if (!changed)
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

	private static byte ScaleButtonBrightness(byte brightness)
	{
		// Legacy API brightness is 0-127; unified packet button brightness behaves as a small range.
		return (byte)Math.Clamp(brightness / 4, 0, 31);
	}

	private static byte EncodePadColor(PadColor color)
	{
		var r = color.R;
		var g = color.G;
		var b = color.B;

		if (r == 0 && g == 0 && b == 0)
		{
			return 0;
		}

		var bright = (byte)Math.Clamp(Math.Max(r, Math.Max(g, b)) >> 6, 0, 3);

		if (r == g && g == b)
		{
			return (byte)((0xFF << 2) | (bright & 0x3));
		}

		var max = Math.Max(r, Math.Max(g, b));
		var min = Math.Min(r, Math.Min(g, b));
		var delta = max - min;
		if (delta == 0)
		{
			return bright;
		}

		var h = 0;
		if (max == r)
		{
			h = ((g - b) * 42) / delta;
		}
		else if (max == g)
		{
			h = (((b - r) * 42) / delta) + 85;
		}
		else
		{
			h = (((r - g) * 42) / delta) + 171;
		}

		if (h < 0)
		{
			h += 255;
		}

		var hue = (byte)((h / 16) + 1);
		return (byte)((hue << 2) | (bright & 0x3));
	}
}