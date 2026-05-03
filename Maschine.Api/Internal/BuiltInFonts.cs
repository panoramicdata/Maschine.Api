using System.Text;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;

namespace Maschine.Api.Internal;

internal static class BuiltInFonts
{
	internal static readonly IFont FixedClassic = new FixedClassicFont();
	internal static readonly IFont FixedThin = new FixedThinFont();
	internal static readonly IFont ProportionalClassic = new ProportionalClassicFont();
	internal static readonly IFont ProportionalThin = new ProportionalThinFont();

	private sealed class FixedClassicFont : IFont
	{
		public string Name => "FixedClassic";
		public int Height => DisplayFont.Font8Height;
		public bool IsMonospace => true;
		public int? FixedWidth => DisplayFont.Font8Width;

		public bool TryGetGlyph(Rune rune, out FontGlyph glyph)
		{
			if (rune.Value < 0x20 || rune.Value > 0x7E)
			{
				glyph = default!;
				return false;
			}

			var span = DisplayFont.GetGlyph8x8((char)rune.Value);
			var rows = span.ToArray();
			glyph = new FontGlyph(DisplayFont.Font8Width, DisplayFont.Font8Height, rows);
			return true;
		}
	}

	private sealed class FixedThinFont : IFont
	{
		public string Name => "FixedThin";
		public int Height => DisplayFont.Font4Height;
		public bool IsMonospace => true;
		public int? FixedWidth => DisplayFont.Font4Width;

		public bool TryGetGlyph(Rune rune, out FontGlyph glyph)
		{
			if (rune.Value < 0x20 || rune.Value > 0x7E)
			{
				glyph = default!;
				return false;
			}

			var span = DisplayFont.GetGlyph4x4((char)rune.Value);
			var rows = span.ToArray();
			glyph = new FontGlyph(DisplayFont.Font4Width, DisplayFont.Font4Height, rows);
			return true;
		}
	}

	private sealed class ProportionalThinFont : IFont
	{
		public string Name => "ProportionalThin";
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

			var span = DisplayFont.GetGlyph4x4((char)rune.Value);
			var rows = span.ToArray();
			var width = ComputeTrimmedWidth(rows);
			if (rune.Value == 0x20)
			{
				width = 2;
			}

			glyph = new FontGlyph(width, DisplayFont.Font4Height, TrimRows(rows, width));
			return true;
		}

		private static int ComputeTrimmedWidth(byte[] rows)
		{
			var highestSetBit = -1;
			for (var r = 0; r < rows.Length; r++)
			{
				for (var bit = 3; bit >= 0; bit--)
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

		private static byte[] TrimRows(byte[] rows, int width)
		{
			var mask = (1 << width) - 1;
			var trimmed = new byte[rows.Length];
			for (var i = 0; i < rows.Length; i++)
			{
				trimmed[i] = (byte)(rows[i] & mask);
			}

			return trimmed;
		}
	}

	private sealed class ProportionalClassicFont : IFont
	{
		public string Name => "ProportionalClassic";
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

			var span = DisplayFont.GetGlyph8x8((char)rune.Value);
			var rows = span.ToArray();
			var width = ComputeTrimmedWidth(rows, 8);
			if (rune.Value == 0x20)
			{
				width = 4;
			}

			glyph = new FontGlyph(width, DisplayFont.Font8Height, TrimRows(rows, width));
			return true;
		}
	}

	private static int ComputeTrimmedWidth(byte[] rows, int maxBits)
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

	private static byte[] TrimRows(byte[] rows, int width)
	{
		var mask = width >= 8 ? 0xFF : (1 << width) - 1;
		var trimmed = new byte[rows.Length];
		for (var i = 0; i < rows.Length; i++)
		{
			trimmed[i] = (byte)(rows[i] & mask);
		}

		return trimmed;
	}
}
