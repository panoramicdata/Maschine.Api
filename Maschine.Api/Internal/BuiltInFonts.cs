using System.Text;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;

namespace Maschine.Api.Internal;

internal static class BuiltInFonts
{
	internal static readonly IFont Proportional4 = new Proportional4Font();
	internal static readonly IFont Proportional8 = new Proportional8Font();
	internal static readonly IFont Proportional8Bold = new Proportional8BoldFont();
	internal static readonly IFont Proportional12 = new Proportional12Font();
	internal static readonly IFont Proportional12Bold = new Proportional12BoldFont();

	private sealed class Proportional4Font : IFont
	{
		public string Name => "Proportional4";
		public int Height => DisplayFont.Font4Height;
		public bool IsMonospace => false;
		public int? FixedWidth => null;

		public bool TryGetGlyph(Rune rune, out FontGlyph glyph)
		{
			if (rune.Value < 0x20 || rune.Value > 0x7E)
			{
				glyph = default!;
				return false;
			}

			var rows = ToUShortRows(DisplayFont.GetGlyph4x4((char)rune.Value));
			var width = ComputeTrimmedWidth(rows, 4);
			if (rune.Value == 0x20)
			{
				width = 2;
			}

			glyph = new FontGlyph(width, DisplayFont.Font4Height, TrimRows(rows, width));
			return true;
		}
	}

	private sealed class Proportional8Font : IFont
	{
		public string Name => "Proportional8";
		public int Height => DisplayFont.Font8Height;
		public bool IsMonospace => false;
		public int? FixedWidth => null;

		public bool TryGetGlyph(Rune rune, out FontGlyph glyph)
		{
			if (rune.Value < 0x20 || rune.Value > 0x7E)
			{
				glyph = default!;
				return false;
			}

			var rows = ToUShortRows(DisplayFont.GetGlyph8x8Light((char)rune.Value));
			var width = ComputeTrimmedWidth(rows, 8);
			if (rune.Value == 0x20)
			{
				width = 4;
			}

			glyph = new FontGlyph(width, DisplayFont.Font8Height, TrimRows(rows, width));
			return true;
		}
	}

	private sealed class Proportional8BoldFont : IFont
	{
		public string Name => "Proportional8Bold";
		public int Height => DisplayFont.Font8Height;
		public bool IsMonospace => false;
		public int? FixedWidth => null;

		public bool TryGetGlyph(Rune rune, out FontGlyph glyph)
		{
			if (rune.Value < 0x20 || rune.Value > 0x7E)
			{
				glyph = default!;
				return false;
			}

			var rows = ToUShortRows(DisplayFont.GetGlyph8x8((char)rune.Value));
			var width = ComputeTrimmedWidth(rows, 8);
			if (rune.Value == 0x20)
			{
				width = 4;
			}

			glyph = new FontGlyph(width, DisplayFont.Font8Height, TrimRows(rows, width));
			return true;
		}
	}

	private sealed class Proportional12Font : IFont
	{
		public string Name => "Proportional12";
		public int Height => DisplayFont.Font12Height;
		public bool IsMonospace => false;
		public int? FixedWidth => null;

		public bool TryGetGlyph(Rune rune, out FontGlyph glyph)
		{
			if (rune.Value < 0x20 || rune.Value > 0x7E)
			{
				glyph = default!;
				return false;
			}

			var rows = DisplayFont.GetGlyph12Regular((char)rune.Value).ToArray();
			var width = ComputeTrimmedWidth(rows, 16);
			if (rune.Value == 0x20)
			{
				width = 5;
			}

			glyph = new FontGlyph(width, DisplayFont.Font12Height, TrimRows(rows, width));
			return true;
		}
	}

	private sealed class Proportional12BoldFont : IFont
	{
		public string Name => "Proportional12Bold";
		public int Height => DisplayFont.Font12Height;
		public bool IsMonospace => false;
		public int? FixedWidth => null;

		public bool TryGetGlyph(Rune rune, out FontGlyph glyph)
		{
			if (rune.Value < 0x20 || rune.Value > 0x7E)
			{
				glyph = default!;
				return false;
			}

			var rows = DisplayFont.GetGlyph12Bold((char)rune.Value).ToArray();
			var width = ComputeTrimmedWidth(rows, 16);
			if (rune.Value == 0x20)
			{
				width = 5;
			}

			glyph = new FontGlyph(width, DisplayFont.Font12Height, TrimRows(rows, width));
			return true;
		}
	}

	private static int ComputeTrimmedWidth(ushort[] rows, int maxBits)
	{
		var highestSetBit = -1;
		for (var r = 0; r < rows.Length; r++)
		{
			for (var bit = maxBits - 1; bit >= 0; bit--)
			{
				if (((rows[r] >> bit) & 1) != 0)
				{
					highestSetBit = Math.Max(highestSetBit, bit);
					break;
				}
			}
		}

		return highestSetBit < 0 ? 1 : highestSetBit + 1;
	}

	private static ushort[] TrimRows(ushort[] rows, int width)
	{
		var mask = width >= 16 ? 0xFFFF : (1 << width) - 1;
		var trimmed = new ushort[rows.Length];
		for (var i = 0; i < rows.Length; i++)
		{
			trimmed[i] = (ushort)(rows[i] & mask);
		}

		return trimmed;
	}

	private static ushort[] ToUShortRows(ReadOnlySpan<byte> rows)
	{
		var widened = new ushort[rows.Length];
		for (var i = 0; i < rows.Length; i++)
		{
			widened[i] = rows[i];
		}

		return widened;
	}
}
