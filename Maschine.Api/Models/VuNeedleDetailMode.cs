namespace Maschine.Api.Models;

/// <summary>
/// Controls how much fixed background detail is rendered for needle-style VU widgets.
/// </summary>
public enum VuNeedleDetailMode
{
	/// <summary>
	/// Choose the most suitable rendering automatically for the zone size.
	/// </summary>
	Auto = 0,

	/// <summary>
	/// Render a minimal needle-only presentation.
	/// </summary>
	Simple = 1,

	/// <summary>
	/// Render additional tick marks when there is enough room.
	/// </summary>
	Detailed = 2,
}
