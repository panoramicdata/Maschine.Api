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
	private readonly PadState[] _states;

	/// <inheritdoc/>
	public event EventHandler<PadState>? PadChanged;

	internal MaschinePads(IHidDevice device)
	{
		_device = device;
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
		var report = MikroMk3Protocol.BuildSinglePadColorReport(padIndex, color);
		return _device.WriteAsync(report, cancellationToken);
	}

	/// <inheritdoc/>
	public Task SetAllColorsAsync(PadColor color, CancellationToken cancellationToken = default)
	{
		var report = MikroMk3Protocol.BuildAllPadsColorReport(color);
		return _device.WriteAsync(report, cancellationToken);
	}

	/// <summary>
	/// Called by <see cref="MaschineClient"/> when a pad-pressure report is received.
	/// Updates internal state and raises <see cref="PadChanged"/> for any changed pads.
	/// </summary>
	internal void ApplyReport(byte[] report)
	{
		var newStates = MikroMk3Protocol.ParsePadPressureReport(report);
		for (var i = 0; i < newStates.Count; i++)
		{
			if (_states[i].Pressure != newStates[i].Pressure)
			{
				_states[i] = newStates[i];
				PadChanged?.Invoke(this, _states[i]);
			}
		}
	}
}
