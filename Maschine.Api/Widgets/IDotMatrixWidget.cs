using Maschine.Api.Models;

namespace Maschine.Api.Widgets;

/// <summary>
/// Common contract for dot-matrix widgets.
/// </summary>
public interface IDotMatrixWidget
{
	/// <summary>Stable widget identifier within a dashboard.</summary>
	string Id { get; }
	/// <summary>Widget display zone in pixels.</summary>
	DisplayZone Zone { get; set; }
	/// <summary>
	/// Inverts colors for this widget (white background, black foreground).
	/// </summary>
	bool Invert { get; set; }
}
