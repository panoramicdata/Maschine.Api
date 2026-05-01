namespace Maschine.Api.Internal;

/// <summary>Represents an open connection to a HID device. Abstracted for testability.</summary>
internal interface IHidDevice : IDisposable
{
	/// <summary>Maximum output report length in bytes (including the leading report-ID byte).</summary>
	int MaxOutputReportLength { get; }

	/// <summary>Maximum feature report length in bytes (including the leading report-ID byte).</summary>
	int MaxFeatureReportLength { get; }

	/// <summary>Reads the next input report from the device.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Raw report bytes including the report ID in the first byte.</returns>
	Task<byte[]> ReadAsync(CancellationToken cancellationToken);

	/// <summary>Writes an output report to the device.</summary>
	/// <param name="data">Raw report bytes including the report ID in the first byte.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task WriteAsync(byte[] data, CancellationToken cancellationToken);

	/// <summary>Sends a feature report to the device via <c>HidD_SetFeature</c>.</summary>
	/// <param name="data">Raw report bytes including the report ID in the first byte.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task WriteFeatureAsync(byte[] data, CancellationToken cancellationToken);
}
