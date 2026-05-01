using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Manages button state and LED brightness for the Maschine Mikro MK3.
/// </summary>
internal sealed class MaschineButtons : IButtons
{
	private readonly IHidDevice _device;
	private readonly MikroMk3UnifiedLights _unifiedLights;
	private readonly ButtonState[] _states;
	private bool _buttonLedUnsupported;

	/// <inheritdoc/>
	public event EventHandler<ButtonState>? ButtonChanged;

	internal MaschineButtons(IHidDevice device, MikroMk3UnifiedLights unifiedLights)
	{
		_device = device;
		_unifiedLights = unifiedLights;
		_states = new ButtonState[MaschineDeviceConstants.MikroMk3ButtonCount];
		for (var i = 0; i < _states.Length; i++)
		{
			_states[i] = new ButtonState(i, false);
		}
	}

	/// <inheritdoc/>
	public IReadOnlyList<ButtonState> GetStates() => _states;

	/// <inheritdoc/>
	public ButtonState GetState(int buttonIndex)
	{
		if (buttonIndex < 0 || buttonIndex >= MaschineDeviceConstants.MikroMk3ButtonCount)
		{
			throw new ArgumentOutOfRangeException(nameof(buttonIndex), buttonIndex,
				$"Button index must be 0-{MaschineDeviceConstants.MikroMk3ButtonCount - 1}.");
		}

		return _states[buttonIndex];
	}

	/// <inheritdoc/>
	public Task SetLedAsync(int buttonIndex, byte brightness, CancellationToken cancellationToken = default)
	{
		var report = MikroMk3Protocol.BuildButtonLedReport(buttonIndex, brightness);
		return WriteSingleButtonLedReportAsync(report, buttonIndex, brightness, cancellationToken);
	}

	/// <inheritdoc/>
	public Task SetAllLedsAsync(byte brightness, CancellationToken cancellationToken = default)
	{
		var report = MikroMk3Protocol.BuildAllButtonLedsReport(brightness);
		return WriteAllButtonLedsReportAsync(report, brightness, cancellationToken);
	}

	private async Task WriteSingleButtonLedReportAsync(byte[] report, int buttonIndex, byte brightness, CancellationToken cancellationToken)
	{
		if (_buttonLedUnsupported || _unifiedLights.IsEnabled)
		{
			await _unifiedLights.SetButtonBrightnessAsync(buttonIndex, brightness, cancellationToken)
				.ConfigureAwait(false);
			return;
		}

		try
		{
			await _device.WriteAsync(report, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (IsUnsupportedButtonLedError(ex))
		{
			_buttonLedUnsupported = true;
			_unifiedLights.Enable();
			await _unifiedLights.SetButtonBrightnessAsync(buttonIndex, brightness, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	private async Task WriteAllButtonLedsReportAsync(byte[] report, byte brightness, CancellationToken cancellationToken)
	{
		if (_buttonLedUnsupported || _unifiedLights.IsEnabled)
		{
			await _unifiedLights.SetAllButtonBrightnessAsync(brightness, cancellationToken).ConfigureAwait(false);
			return;
		}

		try
		{
			await _device.WriteAsync(report, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (IsUnsupportedButtonLedError(ex))
		{
			_buttonLedUnsupported = true;
			_unifiedLights.Enable();
			await _unifiedLights.SetAllButtonBrightnessAsync(brightness, cancellationToken).ConfigureAwait(false);
		}
	}

	private static bool IsUnsupportedButtonLedError(Exception ex)
	{
		for (Exception? current = ex; current is not null; current = current.InnerException)
		{
			var message = current.Message;
			if (message.Contains("parameter is incorrect", StringComparison.OrdinalIgnoreCase)
				|| message.Contains("SetFeature failed", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Called by <see cref="MaschineClient"/> when a button report is received.
	/// Updates internal state and raises <see cref="ButtonChanged"/> for any changed buttons.
	/// </summary>
	internal void ApplyReport(byte[] report)
	{
		var newStates = MikroMk3Protocol.ParseButtonReport(report);
		for (var i = 0; i < newStates.Count; i++)
		{
			if (_states[i].IsPressed != newStates[i].IsPressed)
			{
				_states[i] = newStates[i];
				ButtonChanged?.Invoke(this, _states[i]);
			}
		}
	}
}
