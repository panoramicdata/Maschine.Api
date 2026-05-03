using System.Text;

namespace Maschine.Api.Models;

/// <summary>
/// Transport-related Unicode symbols for media/hardware UIs.
/// Values are UTF-16 code units matching Unicode code points in the BMP.
/// </summary>
public enum TransportSymbol : ushort
{
	/// <summary>U+25B6 BLACK RIGHT-POINTING TRIANGLE.</summary>
	Play = 0x25B6,

	/// <summary>U+23F8 DOUBLE VERTICAL BAR.</summary>
	Pause = 0x23F8,

	/// <summary>U+23F9 BLACK SQUARE FOR STOP.</summary>
	Stop = 0x23F9,

	/// <summary>U+23EA BLACK LEFT-POINTING DOUBLE TRIANGLE.</summary>
	Rewind = 0x23EA,

	/// <summary>U+23E9 BLACK RIGHT-POINTING DOUBLE TRIANGLE.</summary>
	FastForward = 0x23E9,

	/// <summary>U+23FA BLACK CIRCLE FOR RECORD.</summary>
	Record = 0x23FA,
}

/// <summary>
/// Helpers for converting <see cref="TransportSymbol"/> values to text runes.
/// </summary>
public static class TransportSymbolExtensions
{
	/// <summary>
	/// Converts a transport symbol to a Unicode rune.
	/// </summary>
	public static Rune ToRune(this TransportSymbol symbol)
		=> new((int)symbol);
}
