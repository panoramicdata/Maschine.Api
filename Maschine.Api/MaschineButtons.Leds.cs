using Maschine.Api.Internal;

namespace Maschine.Api;

/// <summary>
/// LED writes for <see cref="MaschineButtons"/>, including the fallback to unified lighting
/// on devices that reject the per-button report.
/// </summary>
internal sealed partial class MaschineButtons
{
	private bool _buttonLedUnsupported;

	/// <inheritdoc/>
	public Task SetLedAsync(int buttonIndex, byte brightness, CancellationToken cancellationToken = default)
	{
		ThrowIfLibraryManaged(buttonIndex);
		SyncManagedKeyState(buttonIndex, brightness);
		return SetLedInternalAsync(buttonIndex, brightness, cancellationToken);
	}

	private Task SetLedInternalAsync(int buttonIndex, byte brightness, CancellationToken cancellationToken)
	{
		var scaled = _brightness.Scale(brightness);
		var report = MikroMk3Protocol.BuildButtonLedReport(buttonIndex, scaled);
		return WriteLedReportAsync(report, ct => _unifiedLights.SetButtonBrightnessAsync(buttonIndex, scaled, ct), cancellationToken);
	}

	/// <inheritdoc/>
	public Task SetAllLedsAsync(byte brightness, CancellationToken cancellationToken = default)
	{
		if (!_allowExternalLedOverrides && HasAnyLibraryManagedKeys())
		{
			throw new InvalidOperationException("One or more keys are in a managed key mode; individual/all-button LED writes are disabled for managed keys.");
		}

		SyncAllManagedKeyStates(brightness);
		return SetAllLedsInternalAsync(brightness, cancellationToken);
	}

	private Task SetAllLedsInternalAsync(byte brightness, CancellationToken cancellationToken)
	{
		var scaled = _brightness.Scale(brightness);
		var report = MikroMk3Protocol.BuildAllButtonLedsReport(scaled);
		return WriteLedReportAsync(report, ct => _unifiedLights.SetAllButtonBrightnessAsync(scaled, ct), cancellationToken);
	}

	internal Task SetAllLedsForShutdownAsync(byte brightness, CancellationToken cancellationToken = default)
		=> SetAllLedsInternalAsync(brightness, cancellationToken);

	/// <inheritdoc/>
	public Task SetOnOffAsync(int buttonIndex, bool isOn, CancellationToken cancellationToken = default)
		=> SetLedAsync(buttonIndex, isOn ? (byte)255 : (byte)0, cancellationToken);

	/// <inheritdoc/>
	public Task SetAllOnOffAsync(bool isOn, CancellationToken cancellationToken = default)
		=> SetAllLedsAsync(isOn ? (byte)255 : (byte)0, cancellationToken);

	private async Task WriteLedReportAsync(byte[] report, Func<CancellationToken, Task> unifiedWrite, CancellationToken cancellationToken)
	{
		if (_buttonLedUnsupported || _unifiedLights.IsEnabled)
		{
			await unifiedWrite(cancellationToken).ConfigureAwait(false);
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
			await unifiedWrite(cancellationToken).ConfigureAwait(false);
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
}
