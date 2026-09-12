using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>
/// Pixel primitives for the 1-bit dot-matrix buffer: the bounds check, the byte/bit
/// addressing, and the shapes drawn directly from them.
/// </summary>
internal static class DotMatrixCanvas
{
	internal const int DisplayWidth = DisplayFont.DisplayWidth;

	internal const int DisplayHeight = DisplayFont.DisplayHeight;

	internal const int RowStride = DisplayWidth / 8;

	internal static void FillZone(byte[] bitmap, DisplayZone zone, bool on)
	{
		for (var y = zone.Y; y < zone.Bottom; y++)
		{
			for (var x = zone.X; x < zone.Right; x++)
			{
				SetPixel(bitmap, x, y, on);
			}
		}
	}

	/// <summary>Sets every pixel in the inclusive rectangle x0..x1 by y0..y1.</summary>
	internal static void FillRect(byte[] bitmap, int x0, int x1, int y0, int y1, bool on)
	{
		for (var y = y0; y <= y1; y++)
		{
			for (var x = x0; x <= x1; x++)
			{
				SetPixel(bitmap, x, y, on);
			}
		}
	}

	internal static double DegreesToRadians(double degrees) => (Math.PI / 180.0) * degrees;

	internal static void DrawLine(byte[] bitmap, int x0, int y0, int x1, int y1, bool on)
	{
		var dx = Math.Abs(x1 - x0);
		var sx = x0 < x1 ? 1 : -1;
		var dy = -Math.Abs(y1 - y0);
		var sy = y0 < y1 ? 1 : -1;
		var err = dx + dy;

		while (true)
		{
			SetPixel(bitmap, x0, y0, on);
			if (x0 == x1 && y0 == y1)
			{
				break;
			}

			var e2 = 2 * err;
			if (e2 >= dy)
			{
				err += dy;
				x0 += sx;
			}

			if (e2 <= dx)
			{
				err += dx;
				y0 += sy;
			}
		}
	}

	internal static void SetPixel(byte[] bitmap, DisplayZone zone, int x, int y, bool on)
	{
		if (x < zone.X || x >= zone.Right || y < zone.Y || y >= zone.Bottom)
		{
			return;
		}

		SetPixel(bitmap, x, y, on);
	}

	internal static void SetPixel(byte[] bitmap, int x, int y, bool on)
	{
		if (x < 0 || x >= DisplayWidth || y < 0 || y >= DisplayHeight)
		{
			return;
		}

		var index = (y * RowStride) + (x / 8);
		var bit = 7 - (x % 8);
		var mask = (byte)(1 << bit);
		if (on)
		{
			bitmap[index] |= mask;
		}
		else
		{
			bitmap[index] &= (byte)~mask;
		}
	}
}
