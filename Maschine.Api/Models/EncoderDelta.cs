namespace Maschine.Api.Models;

/// <summary>
/// Represents the incremental change of a rotary encoder.
/// </summary>
/// <param name="Index">Zero-based encoder index.</param>
/// <param name="Delta">Signed rotation delta; positive = clockwise, negative = counter-clockwise.</param>
public readonly record struct EncoderDelta(int Index, int Delta);
