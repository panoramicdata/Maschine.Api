using Maschine.Api.Models;

namespace Maschine.Api.Interfaces;

/// <summary>
/// Controls the touch-strip LED indicators on the Maschine Mikro MK3.
/// </summary>
/// <remarks>
/// The touch strip has <see cref="MaschineDeviceConstants.MikroMk3TouchStripLedCount"/> physical LEDs
/// (positions 0 = left/bottom to 24 = right/top). They share the same brightness encoding
/// as button LEDs (0 = off, 127 = maximum).
/// </remarks>
public interface ITouchStrip
{
	/// <summary>Sets the brightness of a single touch-strip LED.</summary>
	/// <param name="position">Zero-based LED position (0–<see cref="MaschineDeviceConstants.MikroMk3TouchStripLedCount"/> − 1).</param>
	/// <param name="brightness">Brightness level (0 = off, 127 = maximum).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetLedAsync(int position, byte brightness, CancellationToken cancellationToken);

	/// <inheritdoc cref="SetLedAsync(int, byte, CancellationToken)"/>
	Task SetLedAsync(int position, byte brightness) => SetLedAsync(position, brightness, CancellationToken.None);

	/// <summary>Sets all touch-strip LEDs to the same brightness.</summary>
	/// <param name="brightness">Brightness level (0 = off, 127 = maximum).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetAllLedsAsync(byte brightness, CancellationToken cancellationToken);

	/// <inheritdoc cref="SetAllLedsAsync(byte, CancellationToken)"/>
	Task SetAllLedsAsync(byte brightness) => SetAllLedsAsync(brightness, CancellationToken.None);

	/// <summary>
	/// Sets the brightness of every touch-strip LED in one write.
	/// </summary>
	/// <param name="brightnessValues">
	/// Exactly <see cref="MaschineDeviceConstants.MikroMk3TouchStripLedCount"/> values,
	/// one per position (0 = off, 127 = maximum each).
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetLedsAsync(IReadOnlyList<byte> brightnessValues, CancellationToken cancellationToken);

	/// <inheritdoc cref="SetLedsAsync(IReadOnlyList{byte}, CancellationToken)"/>
	Task SetLedsAsync(IReadOnlyList<byte> brightnessValues) => SetLedsAsync(brightnessValues, CancellationToken.None);

	/// <summary>
	/// Sets the colour of every touch-strip LED in one write.
	/// </summary>
	/// <param name="colors">
	/// Exactly <see cref="MaschineDeviceConstants.MikroMk3TouchStripLedCount"/> colours,
	/// one per position. Use <see cref="PadColor.Off"/> to turn an LED off.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetLedsAsync(IReadOnlyList<PadColor> colors, CancellationToken cancellationToken);

	/// <inheritdoc cref="SetLedsAsync(IReadOnlyList{PadColor}, CancellationToken)"/>
	Task SetLedsAsync(IReadOnlyList<PadColor> colors) => SetLedsAsync(colors, CancellationToken.None);
}
