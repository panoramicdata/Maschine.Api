namespace Maschine.Api.Models;

/// <summary>
/// Represents the pressure state of a single pad.
/// </summary>
/// <param name="Index">Zero-based pad index (0–15).</param>
/// <param name="Pressure">Pressure value (0–4095 for 12-bit ADC).</param>
public readonly record struct PadState(int Index, int Pressure)
{
	/// <summary>True when the pad is actively pressed (pressure > 0).</summary>
	public bool IsPressed => Pressure > 0;
}
