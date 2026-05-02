using System.Threading;

namespace Maschine.Api.Internal;

/// <summary>
/// Experimental dot-matrix display writer for Maschine Mikro MK3.
/// Sends two monochrome 256-byte pages (top and bottom) using 9-byte headers.
/// </summary>
internal sealed class MikroMk3DotMatrixDisplay : IDisposable
{
	private const int PixelCountPerSection = 256;
	private static readonly byte[] s_topHeader = [0xE0, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x02, 0x00];
	private static readonly byte[] s_bottomHeader = [0xE0, 0x00, 0x00, 0x02, 0x00, 0x80, 0x00, 0x02, 0x00];

	private readonly IHidDevice _device;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private bool _disposed;

	internal MikroMk3DotMatrixDisplay(IHidDevice device)
	{
		_device = device;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_gate.Dispose();
	}

	internal Task SetTestPatternAsync(CancellationToken cancellationToken)
	{
		var top = new byte[PixelCountPerSection];
		var bottom = new byte[PixelCountPerSection];
		Array.Fill(top, (byte)0xFF);
		Array.Fill(bottom, (byte)0x00);

		return WriteSectionsAsync(top, bottom, cancellationToken);
	}

	internal Task ClearAsync(CancellationToken cancellationToken)
	{
		var top = new byte[PixelCountPerSection];
		var bottom = new byte[PixelCountPerSection];
		return WriteSectionsAsync(top, bottom, cancellationToken);
	}

	internal Task ClearWithFallbackAsync(CancellationToken cancellationToken)
	{
		var top = new byte[PixelCountPerSection];
		var bottom = new byte[PixelCountPerSection];
		return WriteSectionsWithFallbackAsync(top, bottom, cancellationToken);
	}

	internal Task SetZebraLinesAsync(CancellationToken cancellationToken)
		=> SetZebraLinesAsync(0, cancellationToken);

	internal Task SetZebraLinesAsync(int phase, CancellationToken cancellationToken)
	{
		var top = new byte[PixelCountPerSection];
		var bottom = new byte[PixelCountPerSection];

		for (var i = 0; i < PixelCountPerSection; i++)
		{
			// Two-bit diagonal slash that can be shifted via phase for animation.
			var bit = (i + phase) & 7;
			var value = (byte)((1 << bit) | (1 << ((bit + 1) & 7)));
			top[i] = value;
			bottom[i] = value;
		}

		return WriteSectionsAsync(top, bottom, cancellationToken);
	}

	private async Task WriteSectionsAsync(byte[] topPixels, byte[] bottomPixels, CancellationToken cancellationToken)
	{
		if (topPixels.Length != PixelCountPerSection)
		{
			throw new ArgumentException($"Top section must be {PixelCountPerSection} bytes.", nameof(topPixels));
		}

		if (bottomPixels.Length != PixelCountPerSection)
		{
			throw new ArgumentException($"Bottom section must be {PixelCountPerSection} bytes.", nameof(bottomPixels));
		}

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var top = BuildPacket(s_topHeader, topPixels);
			var bottom = BuildPacket(s_bottomHeader, bottomPixels);

			await _device.WriteAsync(top, cancellationToken).ConfigureAwait(false);
			await _device.WriteAsync(bottom, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task WriteSectionsWithFallbackAsync(byte[] topPixels, byte[] bottomPixels, CancellationToken cancellationToken)
	{
		if (topPixels.Length != PixelCountPerSection)
		{
			throw new ArgumentException($"Top section must be {PixelCountPerSection} bytes.", nameof(topPixels));
		}

		if (bottomPixels.Length != PixelCountPerSection)
		{
			throw new ArgumentException($"Bottom section must be {PixelCountPerSection} bytes.", nameof(bottomPixels));
		}

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var top = BuildPacket(s_topHeader, topPixels);
			var bottom = BuildPacket(s_bottomHeader, bottomPixels);

			await _device.WriteAsync(top, cancellationToken).ConfigureAwait(false);
			await _device.WriteAsync(bottom, cancellationToken).ConfigureAwait(false);

			// Some firmware revisions are more reliable when a mirrored feature write is sent on shutdown.
			try
			{
				await _device.WriteFeatureAsync(top, cancellationToken).ConfigureAwait(false);
				await _device.WriteFeatureAsync(bottom, cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				// Best-effort fallback only.
			}

			await _device.WriteAsync(top, cancellationToken).ConfigureAwait(false);
			await _device.WriteAsync(bottom, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	private static byte[] BuildPacket(byte[] header, byte[] pixels)
	{
		var packet = new byte[header.Length + pixels.Length];
		Buffer.BlockCopy(header, 0, packet, 0, header.Length);
		Buffer.BlockCopy(pixels, 0, packet, header.Length, pixels.Length);
		return packet;
	}
}
