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
}
