using Maschine.Api.Models;

namespace Maschine.Api.Interfaces;

/// <summary>
/// Provides read access to button states and raised events when buttons change.
/// </summary>
public interface IButtons
{
	/// <summary>Raised when any button is pressed or released.</summary>
	event EventHandler<ButtonState> ButtonChanged;

	/// <summary>Raised when any button transitions to pressed.</summary>
	event EventHandler<ButtonState>? ButtonPressed;

	/// <summary>Raised when any button transitions to released.</summary>
	event EventHandler<ButtonState>? ButtonReleased;

	/// <summary>
	/// Raised when the volume encoder's capacitive touch sensor or absolute position changes.
	/// The <see cref="EncoderTouchState.KnobValue"/> is an absolute 0–15 position.
	/// </summary>
	event EventHandler<EncoderTouchState>? EncoderTouchChanged;

	/// <summary>Returns the last known state for every button.</summary>
	IReadOnlyList<ButtonState> GetStates();

	/// <summary>Returns the last known state for a single button.</summary>
	/// <param name="buttonIndex">Zero-based button index (0–<see cref="MaschineDeviceConstants.MikroMk3ButtonCount"/> − 1).</param>
	ButtonState GetState(int buttonIndex);

	/// <summary>Sets the LED brightness for a single button.</summary>
	/// <param name="buttonIndex">Zero-based button index (0–<see cref="MaschineDeviceConstants.MikroMk3ButtonCount"/> − 1).</param>
	/// <param name="brightness">Brightness level (0 = off, 127 = maximum).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetLedAsync(int buttonIndex, byte brightness, CancellationToken cancellationToken = default);

	/// <summary>Sets the LED brightness for all buttons at once.</summary>
	/// <param name="brightness">Brightness level applied to every button LED (0 = off, 127 = maximum).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetAllLedsAsync(byte brightness, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sets a single button LED to a managed on/off state.
	/// </summary>
	/// <param name="buttonIndex">Zero-based button index.</param>
	/// <param name="isOn">True for on, false for off.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetOnOffAsync(int buttonIndex, bool isOn, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sets all button LEDs to a managed on/off state.
	/// </summary>
	/// <param name="isOn">True for on, false for off.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetAllOnOffAsync(bool isOn, CancellationToken cancellationToken = default);
}
