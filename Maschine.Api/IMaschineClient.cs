using Maschine.Api.Interfaces;

namespace Maschine.Api;

/// <summary>
/// Main client for interacting with a connected Maschine controller.
/// </summary>
public interface IMaschineClient : IDisposable
{
	/// <summary>Pad controls — colour, pressure events.</summary>
	IPads Pads { get; }

	/// <summary>Button controls — state, press/release events.</summary>
	IButtons Buttons { get; }

	/// <summary>Encoder events.</summary>
	IEncoders Encoders { get; }

	/// <summary>
	/// Starts the background HID read loop and connects to the device.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task ConnectAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Stops the background read loop and releases the HID device.
	/// </summary>
	Task DisconnectAsync();

	/// <summary>
	/// Experimental: writes a simple top/bottom test pattern to the Mikro MK3 dot-matrix display.
	/// </summary>
	Task SetDotMatrixTestPatternAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Experimental: clears the Mikro MK3 dot-matrix display sections.
	/// </summary>
	Task ClearDotMatrixAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Experimental: writes a zebra-line pattern to the Mikro MK3 dot-matrix display.
	/// </summary>
	Task SetDotMatrixZebraLinesAsync(int phase = 0, CancellationToken cancellationToken = default);
}
