namespace Maschine.Api.Models;

/// <summary>
/// Represents the current touch-sensor state of the volume encoder on the Maschine Mikro MK3.
/// Sourced from the <c>encoder_touched</c> and <c>encoder_value</c> fields in the HID button report.
/// </summary>
/// <param name="IsTouched">True when the user's finger is detected on the knob.</param>
/// <param name="KnobValue">Absolute knob position (0–15).</param>
public readonly record struct EncoderTouchState(bool IsTouched, byte KnobValue);
