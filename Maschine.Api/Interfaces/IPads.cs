using Maschine.Api.Models;

namespace Maschine.Api.Interfaces;

/// <summary>
/// Provides read access to pad pressure states and raised events when pads change.
/// </summary>
public interface IPads
{
	/// <summary>Raised when any pad pressure changes.</summary>
	event EventHandler<PadState> PadChanged;

	/// <summary>Returns the last known state for every pad.</summary>
	IReadOnlyList<PadState> GetStates();

	/// <summary>Returns the last known state for a single pad.</summary>
	/// <param name="padIndex">Zero-based pad index (0–<see cref="MaschineDeviceConstants.MikroMk3PadCount"/> − 1).</param>
	PadState GetState(int padIndex);

	/// <summary>Sets the RGB LED colour for a single pad.</summary>
	/// <param name="padIndex">Zero-based pad index.</param>
	/// <param name="color">Target colour.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetColorAsync(int padIndex, PadColor color, CancellationToken cancellationToken = default);

	/// <summary>Sets the RGB LED colour for all pads at once.</summary>
	/// <param name="color">Target colour applied to every pad.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetAllColorsAsync(PadColor color, CancellationToken cancellationToken = default);
}
