using Maschine.Api.Models;

namespace Maschine.Api.Interfaces;

/// <summary>
/// Provides access to encoder rotation events.
/// </summary>
public interface IEncoders
{
	/// <summary>Raised when any encoder is rotated.</summary>
	event EventHandler<EncoderDelta> EncoderChanged;
}
