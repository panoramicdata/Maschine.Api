using Maschine.Api.Models;

namespace Maschine.Api.Widgets;

/// <summary>
/// Base class for dot-matrix widgets.
/// </summary>
public abstract class DotMatrixWidgetBase : IDotMatrixWidget
{
	/// <inheritdoc/>
	public string Id { get; }
	/// <inheritdoc/>
	public DisplayZone Zone { get; set; }
	/// <inheritdoc/>
	public bool Invert { get; set; }

	/// <summary>
	/// Creates a new widget.
	/// </summary>
	/// <param name="id">Stable widget identifier.</param>
	/// <param name="zone">Widget display zone in pixels.</param>
	/// <param name="invert">Whether to invert widget colors.</param>
	protected DotMatrixWidgetBase(string id, DisplayZone zone, bool invert = false)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			throw new ArgumentException("Widget id is required.", nameof(id));
		}

		Id = id;
		Zone = zone;
		Invert = invert;
	}
}
