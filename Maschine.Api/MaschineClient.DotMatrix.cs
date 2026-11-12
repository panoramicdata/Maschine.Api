using Maschine.Api.Models;
using Maschine.Api.Widgets;

namespace Maschine.Api;

public sealed partial class MaschineClient
{
	/// <inheritdoc/>
	public Task SetDotMatrixTestPatternAsync() => SetDotMatrixTestPatternAsync(CancellationToken.None);

	/// <inheritdoc/>
	public Task SetDotMatrixTestPatternAsync(CancellationToken cancellationToken)
		=> EnsureConnected(_dotMatrixDisplay).SetTestPatternAsync(cancellationToken);

	/// <inheritdoc/>
	public Task ClearDotMatrixAsync() => ClearDotMatrixAsync(CancellationToken.None);

	/// <inheritdoc/>
	public Task ClearDotMatrixAsync(CancellationToken cancellationToken)
		=> EnsureConnected(_dotMatrixDisplay).ClearAsync(cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixZebraLinesAsync() => SetDotMatrixZebraLinesAsync(0, CancellationToken.None);

	/// <inheritdoc/>
	public Task SetDotMatrixZebraLinesAsync(CancellationToken cancellationToken) => SetDotMatrixZebraLinesAsync(0, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixZebraLinesAsync(int phase) => SetDotMatrixZebraLinesAsync(phase, CancellationToken.None);

	/// <inheritdoc/>
	public Task SetDotMatrixZebraLinesAsync(int phase, CancellationToken cancellationToken)
		=> EnsureConnected(_dotMatrixDisplay).SetZebraLinesAsync(phase, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixBitmapAsync(byte[] bitmap) => SetDotMatrixBitmapAsync(bitmap, 0, 0, CancellationToken.None);

	/// <inheritdoc/>
	public Task SetDotMatrixBitmapAsync(byte[] bitmap, CancellationToken cancellationToken) => SetDotMatrixBitmapAsync(bitmap, 0, 0, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixBitmapAsync(byte[] bitmap, int xOffset, int yOffset) => SetDotMatrixBitmapAsync(bitmap, xOffset, yOffset, CancellationToken.None);

	/// <inheritdoc/>
	public Task SetDotMatrixBitmapAsync(byte[] bitmap, int xOffset, int yOffset, CancellationToken cancellationToken)
		=> EnsureConnected(_dotMatrixDisplay).SetBitmapAsync(bitmap, xOffset, yOffset, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixTextAsync(IReadOnlyList<string> lines, DisplayLineMode mode) => SetDotMatrixTextAsync(lines, mode, 0, 0, CancellationToken.None);

	/// <inheritdoc/>
	public Task SetDotMatrixTextAsync(IReadOnlyList<string> lines, DisplayLineMode mode, CancellationToken cancellationToken) => SetDotMatrixTextAsync(lines, mode, 0, 0, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixTextAsync(IReadOnlyList<string> lines, DisplayLineMode mode, int xOffset, int yOffset) => SetDotMatrixTextAsync(lines, mode, xOffset, yOffset, CancellationToken.None);

	/// <inheritdoc/>
	public Task SetDotMatrixTextAsync(IReadOnlyList<string> lines, DisplayLineMode mode, int xOffset, int yOffset, CancellationToken cancellationToken)
		=> EnsureConnected(_dotMatrixDisplay).SetTextAsync(lines, mode, xOffset, yOffset, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixDashboardAsync(DotMatrixDashboard dashboard) => SetDotMatrixDashboardAsync(dashboard, CancellationToken.None);

	/// <inheritdoc/>
	public Task SetDotMatrixDashboardAsync(DotMatrixDashboard dashboard, CancellationToken cancellationToken)
		=> SetDotMatrixBitmapAsync((dashboard ?? throw new ArgumentNullException(nameof(dashboard))).BuildBitmap(), 0, 0, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixWidgetsAsync(IReadOnlyList<IDotMatrixWidget> widgets) => SetDotMatrixWidgetsAsync(widgets, CancellationToken.None);

	/// <inheritdoc/>
	public Task SetDotMatrixWidgetsAsync(IReadOnlyList<IDotMatrixWidget> widgets, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(widgets);
		var dashboard = new DotMatrixDashboard();
		foreach (var widget in widgets)
		{
			dashboard.AddWidget(widget);
		}

		return SetDotMatrixDashboardAsync(dashboard, cancellationToken);
	}

	/// <inheritdoc/>
	public Task RunDotMatrixDashboardLoopAsync(DotMatrixDashboard dashboard) => RunDotMatrixDashboardLoopAsync(dashboard, 30, CancellationToken.None);

	/// <inheritdoc/>
	public Task RunDotMatrixDashboardLoopAsync(DotMatrixDashboard dashboard, CancellationToken cancellationToken) => RunDotMatrixDashboardLoopAsync(dashboard, 30, cancellationToken);

	/// <inheritdoc/>
	public Task RunDotMatrixDashboardLoopAsync(DotMatrixDashboard dashboard, int framesPerSecond) => RunDotMatrixDashboardLoopAsync(dashboard, framesPerSecond, CancellationToken.None);

	/// <inheritdoc/>
	public async Task RunDotMatrixDashboardLoopAsync(DotMatrixDashboard dashboard, int framesPerSecond, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(dashboard);
		if (framesPerSecond <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(framesPerSecond), framesPerSecond, "framesPerSecond must be > 0.");
		}

		var delay = TimeSpan.FromMilliseconds(1000.0 / framesPerSecond);
		byte[]? previousFrame = null;
		while (!cancellationToken.IsCancellationRequested)
		{
			var frame = dashboard.BuildBitmap();
			if (previousFrame is null || !frame.AsSpan().SequenceEqual(previousFrame))
			{
				await SetDotMatrixBitmapAsync(frame, 0, 0, cancellationToken).ConfigureAwait(false);
				previousFrame = frame;
			}

			dashboard.AdvanceFrame(1);
			await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
		}
	}
}
