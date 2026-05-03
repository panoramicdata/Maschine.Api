using Maschine.Api.Models;

namespace Maschine.Api.Widgets;

/// <summary>
/// Equalizer widget rendered as vertical bands.
/// This is a compatibility alias over <see cref="SpectrumWidget"/>.
/// </summary>
public sealed class EqWidget : SpectrumWidget
{
	/// <summary>
	/// Creates an EQ widget.
	/// </summary>
	/// <param name="id">Stable widget identifier.</param>
	/// <param name="zone">Widget display zone in pixels.</param>
	/// <param name="bandLevels">Band levels normalized to 0..1.</param>
	/// <param name="invert">Whether to invert widget colors.</param>
	public EqWidget(string id, DisplayZone zone, IReadOnlyList<float> bandLevels, bool invert = false)
		: base(id, zone, bandLevels, invert)
	{
	}
}
