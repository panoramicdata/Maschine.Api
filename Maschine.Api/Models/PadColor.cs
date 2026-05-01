namespace Maschine.Api.Models;

/// <summary>
/// Represents a 24-bit RGB colour for a pad LED.
/// </summary>
/// <param name="R">Red component (0–255).</param>
/// <param name="G">Green component (0–255).</param>
/// <param name="B">Blue component (0–255).</param>
public readonly record struct PadColor(byte R, byte G, byte B)
{
	/// <summary>No light (off).</summary>
	public static readonly PadColor Off = new(0, 0, 0);

	/// <summary>Full white.</summary>
	public static readonly PadColor White = new(255, 255, 255);

	/// <summary>Full red.</summary>
	public static readonly PadColor Red = new(255, 0, 0);

	/// <summary>Full green.</summary>
	public static readonly PadColor Green = new(0, 255, 0);

	/// <summary>Full blue.</summary>
	public static readonly PadColor Blue = new(0, 0, 255);
}
