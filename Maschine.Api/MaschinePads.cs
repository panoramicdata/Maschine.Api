using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Manages pad state and LED colours for the Maschine Mikro MK3.
/// </summary>
internal sealed class MaschinePads : IPads
{
	private readonly IHidDevice _device;
	private readonly MikroMk3UnifiedLights _unifiedLights;
	private readonly PadState[] _states;

	/// <inheritdoc/>
	public event EventHandler<PadState>? PadChanged;

	internal MaschinePads(IHidDevice device, MikroMk3UnifiedLights unifiedLights)
	{
		_device = device;
		_unifiedLights = unifiedLights;
		_states = new PadState[MaschineDeviceConstants.MikroMk3PadCount];
		for (var i = 0; i < _states.Length; i++)
		{
			_states[i] = new PadState(i, 0);
		}
	}

	/// <inheritdoc/>
	public IReadOnlyList<PadState> GetStates() => _states;

	/// <inheritdoc/>
	public PadState GetState(int padIndex)
	{
		if (padIndex < 0 || padIndex >= MaschineDeviceConstants.MikroMk3PadCount)
		{
			throw new ArgumentOutOfRangeException(nameof(padIndex), padIndex,
				$"Pad index must be 0–{MaschineDeviceConstants.MikroMk3PadCount - 1}.");
		}

		return _states[padIndex];
	}

	/// <inheritdoc/>
	public Task SetColorAsync(int padIndex, PadColor color, CancellationToken cancellationToken = default)
	{
		if (_unifiedLights.IsEnabled)
		{
			return _unifiedLights.SetPadColorAsync(padIndex, color, cancellationToken);
		}

		var report = MikroMk3Protocol.BuildSinglePadColorReport(padIndex, color);
		return _device.WriteAsync(report, cancellationToken);
	}

	/// <inheritdoc/>
	public Task SetAllColorsAsync(PadColor color, CancellationToken cancellationToken = default)
	{
		if (_unifiedLights.IsEnabled)
		{
			return _unifiedLights.SetAllPadColorsAsync(color, cancellationToken);
		}

		var report = MikroMk3Protocol.BuildAllPadsColorReport(color);
		return _device.WriteAsync(report, cancellationToken);
	}

	/// <summary>
	/// Called by <see cref="MaschineClient"/> when a pad-pressure report is received.
	/// Updates internal state and raises <see cref="PadChanged"/> for any changed pads.
	/// </summary>
	internal void ApplyReport(byte[] report)
	{
		var newState = MikroMk3Protocol.ParsePadPressureReport(report);
		var i = newState.Index;
		if (i >= 0 && i < _states.Length && _states[i].Pressure != newState.Pressure)
		{
			_states[i] = newState;
			PadChanged?.Invoke(this, _states[i]);
		}
	}
}
