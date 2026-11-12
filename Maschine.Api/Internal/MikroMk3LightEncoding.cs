using Maschine.Api.Models;

namespace Maschine.Api.Internal;

/// <summary>
/// Encodes brightness and colour into the one-byte light format of the unified Mikro MK3 packet.
/// </summary>
internal static class MikroMk3LightEncoding
{
	// Upper hue bound (degrees) of palette index n + 1; hues past the last bound wrap back to red.
	private static readonly double[] s_hueUpperBounds =
	[
		10, 25, 38, 52, 75, 105, 135, 165, 195, 225, 248, 263, 278, 293, 315, 350,
	];

	internal static byte ScaleButtonBrightness(byte brightness)
	{
		if (brightness == 0)
		{
			return 0;
		}
		// Map brightness 1-255 to intensity 1-3; colour index 1 in high 6 bits.
		// Thresholds are chosen so that 64/127/255 each hit a distinct level.
		byte intensity = brightness >= 170 ? (byte)3 : brightness >= 85 ? (byte)2 : (byte)1;
		return (byte)((1 << 2) | intensity);
	}

	internal static byte EncodePadColor(PadColor color)
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

		return (byte)((HueToPaletteIndex(ComputeHueDegrees(r, g, b, max, delta)) << 2) | intensity);
	}

	/// <summary>Hue of an RGB triple in degrees, 0-360.</summary>
	private static double ComputeHueDegrees(byte r, byte g, byte b, int max, int delta)
	{
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

		return h < 0 ? h + 360 : h;
	}

	/// <summary>
	/// Maps a hue to the NI Mikro MK3 fixed palette indices 1-16:
	/// 1=red, 2=orange, 3=light-orange, 4=warm-yellow, 5=yellow,
	/// 6=lime, 7=green, 8=mint, 9=cyan, 10=turquoise, 11=blue,
	/// 12=plum, 13=violet, 14=purple, 15=magenta, 16=fuchsia.
	/// </summary>
	private static byte HueToPaletteIndex(double hueDegrees)
	{
		for (var i = 0; i < s_hueUpperBounds.Length; i++)
		{
			if (hueDegrees < s_hueUpperBounds[i])
			{
				return (byte)(i + 1);
			}
		}

		return 1;
	}
}
