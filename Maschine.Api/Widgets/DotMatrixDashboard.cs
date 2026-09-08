using Maschine.Api.Internal;
using Maschine.Api.Exceptions;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>
/// Widget-based compositor for the 128x32 dot-matrix display.
/// Widgets are stacked in insertion order and must not overlap.
/// </summary>
public sealed partial class DotMatrixDashboard
{
	private const int DisplayWidth = DisplayFont.DisplayWidth;
	private const int DisplayHeight = DisplayFont.DisplayHeight;
	private const int RowStride = DisplayWidth / 8;
	private static readonly Rune[] s_missingRunes = "[X]".EnumerateRunes().ToArray();

	private readonly List<IDotMatrixWidget> _widgets = [];

	/// <summary>Raised when a widget is added.</summary>
	public event EventHandler<IDotMatrixWidget>? WidgetAdded;
	/// <summary>Raised when a widget is updated or reordered.</summary>
	public event EventHandler<IDotMatrixWidget>? WidgetUpdated;
	/// <summary>Raised when a widget is removed.</summary>
	public event EventHandler<IDotMatrixWidget>? WidgetRemoved;
	/// <summary>Raised when all widgets are cleared.</summary>
	public event EventHandler? WidgetsCleared;
	/// <summary>Raised after a bitmap is composed.</summary>
	public event EventHandler? Rendered;

	/// <summary>Current widget stack in render order.</summary>
	public IReadOnlyList<IDotMatrixWidget> Widgets => _widgets;

	/// <summary>
	/// Adds a widget to the stack.
	/// </summary>
	/// <param name="widget">Widget to add.</param>
	public void AddWidget(IDotMatrixWidget widget)
	{
		ArgumentNullException.ThrowIfNull(widget);
		ValidateZone(widget.Zone);
		EnsureNoOverlap(widget);

		if (_widgets.Any(w => string.Equals(w.Id, widget.Id, StringComparison.Ordinal)))
		{
			throw new InvalidOperationException($"A widget with id '{widget.Id}' already exists.");
		}

		_widgets.Add(widget);
		WidgetAdded?.Invoke(this, widget);
	}

	/// <summary>
	/// Updates a widget in place.
	/// </summary>
	/// <param name="id">Widget id.</param>
	/// <param name="update">Mutation callback.</param>
	public void UpdateWidget(string id, Action<IDotMatrixWidget> update)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(update);

		var widget = _widgets.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.Ordinal))
			?? throw new InvalidOperationException($"Widget '{id}' not found.");

		update(widget);
		ValidateZone(widget.Zone);
		EnsureNoOverlap(widget);
		WidgetUpdated?.Invoke(this, widget);
	}

	/// <summary>
	/// Removes a widget by id.
	/// </summary>
	/// <param name="id">Widget id.</param>
	/// <returns><see langword="true"/> when removed; otherwise <see langword="false"/>.</returns>
	public bool RemoveWidget(string id)
	{
		var index = _widgets.FindIndex(w => string.Equals(w.Id, id, StringComparison.Ordinal));
		if (index < 0)
		{
			return false;
		}

		var removed = _widgets[index];
		_widgets.RemoveAt(index);
		WidgetRemoved?.Invoke(this, removed);
		return true;
	}

	/// <summary>
	/// Removes all widgets.
	/// </summary>
	public void ClearWidgets()
	{
		if (_widgets.Count == 0)
		{
			return;
		}

		_widgets.Clear();
		WidgetsCleared?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Moves a widget to the top of the render stack.
	/// </summary>
	/// <param name="id">Widget id.</param>
	public void BringToFront(string id)
	{
		MoveWidget(id, _widgets.Count - 1);
	}

	/// <summary>
	/// Moves a widget to the bottom of the render stack.
	/// </summary>
	/// <param name="id">Widget id.</param>
	public void SendToBack(string id)
	{
		MoveWidget(id, 0);
	}

	/// <summary>
	/// Composes all widgets into a 512-byte row-packed bitmap.
	/// </summary>
	/// <returns>Bitmap in 32 rows x 16 bytes/row packed format.</returns>
	public byte[] BuildBitmap()
	{
		var bitmap = new byte[DisplayHeight * RowStride];
		foreach (var widget in _widgets)
		{
			RenderWidget(bitmap, widget);
		}

		Rendered?.Invoke(this, EventArgs.Empty);
		return bitmap;
	}

	/// <summary>
	/// Advances animated widget state by one frame.
	/// </summary>
	/// <param name="direction">Animation direction. Positive advances forward; negative advances backward.</param>
	/// <returns><see langword="true"/> when at least one widget state changed.</returns>
	public bool AdvanceFrame(int direction = 1)
	{
		if (direction == 0)
		{
			return false;
		}

		var changed = false;
		foreach (var widget in _widgets)
		{
			if (widget is TextWidget text
				&& (text.OverflowMode == TextOverflowMode.Scroll || text.OverflowMode == TextOverflowMode.Rotate)
				&& text.OverflowStepPixels != 0)
			{
				text.OverflowOffset += text.OverflowStepPixels * Math.Sign(direction);
				changed = true;
			}
		}

		return changed;
	}

	private void MoveWidget(string id, int newIndex)
	{
		var oldIndex = _widgets.FindIndex(w => string.Equals(w.Id, id, StringComparison.Ordinal));
		if (oldIndex < 0)
		{
			throw new InvalidOperationException($"Widget '{id}' not found.");
		}

		newIndex = Math.Clamp(newIndex, 0, _widgets.Count - 1);
		if (oldIndex == newIndex)
		{
			return;
		}

		var item = _widgets[oldIndex];
		_widgets.RemoveAt(oldIndex);
		_widgets.Insert(newIndex, item);
		WidgetUpdated?.Invoke(this, item);
	}

	private static void ValidateZone(DisplayZone zone)
	{
		if (!zone.IsWithin(DisplayWidth, DisplayHeight))
		{
			throw new ArgumentOutOfRangeException(nameof(zone), zone,
				$"Zone must be fully within {DisplayWidth}x{DisplayHeight} display bounds.");
		}
	}

	private void EnsureNoOverlap(IDotMatrixWidget candidate)
	{
		foreach (var existing in _widgets)
		{
			if (ReferenceEquals(existing, candidate))
			{
				continue;
			}

			if (existing.Zone.Intersects(candidate.Zone))
			{
				throw new DashboardLayoutException(
					$"Widget '{candidate.Id}' overlaps existing widget '{existing.Id}'.");
			}
		}
	}
}
