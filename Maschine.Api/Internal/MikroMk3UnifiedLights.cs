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
		if (brightness == 0)
		{
			return 0;
		}
		// Map 0-127 brightness to intensity in the low 2 bits; colour index 1 in high 6 bits.
		byte intensity = brightness >= 85 ? (byte)3 : brightness >= 43 ? (byte)2 : (byte)1;
		return (byte)((1 << 2) | intensity);
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

var max = Math.Max(r, Math.Max(g, b));
byte intensity = max >= 171 ? (byte)2 : max >= 86 ? (byte)1 : (byte)0;

// Near-grayscale (low saturation) -> white (palette index 17).
var min = Math.Min(r, Math.Min(g, b));
var delta = max - min;
if (delta < max / 4)
{
return (byte)((17 << 2) | intensity);
}

// Compute hue in degrees 0-360.
double h;
if (max == r)
{
h = 60.0 * ((double)(g - b) / delta % 6);
}
else if (max == g)
{
h = 60.0 * ((double)(b - r) / delta + 2);
}
else
{
h = 60.0 * ((double)(r - g) / delta + 4);
}

if (h < 0) h += 360;

// Map hue to NI Mikro MK3 fixed palette indices 1-16.
// 1=red, 2=orange, 3=light-orange, 4=warm-yellow, 5=yellow,
// 6=lime, 7=green, 8=mint, 9=cyan, 10=turquoise, 11=blue,
// 12=plum, 13=violet, 14=purple, 15=magenta, 16=fuchsia.
byte colorIndex = h switch
{
< 10  => 1,
< 25  => 2,
< 38  => 3,
< 52  => 4,
< 75  => 5,
< 105 => 6,
< 135 => 7,
< 165 => 8,
< 195 => 9,
< 225 => 10,
< 248 => 11,
< 263 => 12,
< 278 => 13,
< 293 => 14,
< 315 => 15,
< 350 => 16,
_     => 1,
};

return (byte)((colorIndex << 2) | intensity);
}
}
