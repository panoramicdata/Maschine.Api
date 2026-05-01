using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;

namespace Maschine.Api;

/// <summary>
/// Manages encoder events for the Maschine Mikro MK3.
/// </summary>
internal sealed class MaschineEncoders : IEncoders
{
	/// <inheritdoc/>
	public event EventHandler<EncoderDelta>? EncoderChanged;

	/// <summary>
	/// Called by <see cref="MaschineClient"/> when an encoder report is received.
	/// Raises <see cref="EncoderChanged"/> for each encoder that moved.
	/// </summary>
	internal void ApplyReport(byte[] report)
	{
		var deltas = MikroMk3Protocol.ParseEncoderReport(report);
		foreach (var delta in deltas)
		{
			EncoderChanged?.Invoke(this, delta);
		}
	}
}
