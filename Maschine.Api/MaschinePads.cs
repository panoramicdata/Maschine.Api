using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Manages pad state and LED colours for the Maschine Mikro MK3.
/// </summary>
internal sealed class MaschinePads : IPads, IDisposable
{
	private readonly IHidDevice _device;
	private readonly MikroMk3UnifiedLights _unifiedLights;
	private readonly LedBrightnessController _brightness;
	private readonly PadState[] _states;
	private readonly PadColor[] _colors;
	private readonly SemaphoreSlim _writeGate = new(1, 1);
	private bool _disposed;

	/// <inheritdoc/>
	public event EventHandler<PadState>? PadChanged;

	internal MaschinePads(IHidDevice device, MikroMk3UnifiedLights unifiedLights, LedBrightnessController brightness)
	{
		_device = device;
		_unifiedLights = unifiedLights;
		_brightness = brightness;
		_states = new PadState[MaschineDeviceConstants.MikroMk3PadCount];
		_colors = new PadColor[MaschineDeviceConstants.MikroMk3PadCount];
		for (var i = 0; i < _states.Length; i++)
		{
			_states[i] = new PadState(i, 0);
			_colors[i] = PadColor.Off;
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
	public async Task SetColorAsync(int padIndex, PadColor color, CancellationToken cancellationToken = default)
	{
		if (padIndex < 0 || padIndex >= MaschineDeviceConstants.MikroMk3PadCount)
		{
			throw new ArgumentOutOfRangeException(nameof(padIndex), padIndex,
				$"Pad index must be 0–{MaschineDeviceConstants.MikroMk3PadCount - 1}.");
		}

		await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_colors[padIndex] = _brightness.Scale(color);

			if (_unifiedLights.IsEnabled)
			{
				await _unifiedLights.SetPadColorAsync(padIndex, _colors[padIndex], cancellationToken).ConfigureAwait(false);
				return;
			}

			var report = MikroMk3Protocol.BuildPadColorsReport(_colors);
			try
			{
				await _device.WriteAsync(report, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (IsUnsupportedPadLedError(ex))
			{
				_unifiedLights.Enable();
				await _unifiedLights.SetPadColorAsync(padIndex, _colors[padIndex], cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			_writeGate.Release();
		}
	}

	/// <inheritdoc/>
	public async Task SetAllColorsAsync(PadColor color, CancellationToken cancellationToken = default)
	{
		await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var scaled = _brightness.Scale(color);
			for (var i = 0; i < _colors.Length; i++)
			{
				_colors[i] = scaled;
			}

			if (_unifiedLights.IsEnabled)
			{
				await _unifiedLights.SetAllPadColorsAsync(scaled, cancellationToken).ConfigureAwait(false);
				return;
			}

			var report = MikroMk3Protocol.BuildAllPadsColorReport(scaled);
			try
			{
				await _device.WriteAsync(report, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (IsUnsupportedPadLedError(ex))
			{
				_unifiedLights.Enable();
				await _unifiedLights.SetAllPadColorsAsync(scaled, cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			_writeGate.Release();
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_writeGate.Dispose();
	}

	private static bool IsUnsupportedPadLedError(Exception ex)
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
	/// Called by <see cref="MaschineClient"/> when a pad-pressure report is received.
	/// Updates internal state and raises <see cref="PadChanged"/> for any changed pads.
	/// </summary>
	internal void ApplyReport(byte[] report)
	{
		if (report.Length < MikroMk3Protocol.PadPressureReportLength || report[0] != MikroMk3Protocol.PadPressureReportId)
		{
			return;
		}

		var padState = MikroMk3Protocol.ParsePadPressureReport(report);
		var idx = padState.Index;
		if (idx < 0 || idx >= _states.Length)
		{
			return;
		}

		if (_states[idx].Pressure != padState.Pressure)
		{
			_states[idx] = padState;
			PadChanged?.Invoke(this, _states[idx]);
		}
	}
}
