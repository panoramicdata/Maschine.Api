using System.Diagnostics.CodeAnalysis;
using HidSharp;

namespace Maschine.Api.Internal;

/// <summary>
/// Wraps a <see cref="HidStream"/> as <see cref="IHidDevice"/>.
/// Excluded from code coverage as it is a thin hardware-access wrapper.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class HidSharpDevice : IHidDevice
{
	private readonly HidStream _stream;
	private bool _disposed;

	/// <inheritdoc/>
	public int MaxOutputReportLength { get; }

	/// <inheritdoc/>
	public int MaxFeatureReportLength { get; }

	internal HidSharpDevice(HidStream stream, int maxOutputReportLength, int maxFeatureReportLength)
	{
		_stream = stream;
		MaxOutputReportLength = maxOutputReportLength;
		MaxFeatureReportLength = maxFeatureReportLength;
	}

	/// <inheritdoc/>
	public async Task<byte[]> ReadAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		var buffer = new byte[64];
		var bytesRead = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
		if (bytesRead == buffer.Length)
		{
			return buffer;
		}

		var result = new byte[bytesRead];
		Buffer.BlockCopy(buffer, 0, result, 0, bytesRead);
		return result;
	}

	/// <inheritdoc/>
	public async Task WriteAsync(byte[] data, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await _stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task WriteFeatureAsync(byte[] data, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		cancellationToken.ThrowIfCancellationRequested();

		// HidD_SetFeature requires exactly MaxFeatureReportLength bytes.
		if (MaxFeatureReportLength > 0 && data.Length != MaxFeatureReportLength)
		{
			var sized = new byte[MaxFeatureReportLength];
			Buffer.BlockCopy(data, 0, sized, 0, Math.Min(data.Length, MaxFeatureReportLength));
			data = sized;
		}

		_stream.SetFeature(data);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_stream.Dispose();
		_disposed = true;
	}
}
