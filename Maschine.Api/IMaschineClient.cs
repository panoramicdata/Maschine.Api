using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Maschine.Api.Widgets;

namespace Maschine.Api;

/// <summary>
/// Main client for interacting with a connected Maschine controller.
/// </summary>
public interface IMaschineClient : IDisposable
{
	/// <summary>
	/// Global LED brightness scalar (0-100) applied to pad/button writes.
	/// </summary>
	int LedBrightnessPercent { get; set; }

	/// <summary>Pad controls — colour, pressure events.</summary>
	IPads Pads { get; }

	/// <summary>Button controls — state, press/release events.</summary>
	IButtons Buttons { get; }

	/// <summary>Encoder events.</summary>
	IEncoders Encoders { get; }

	/// <summary>Touch-strip LED controls.</summary>
	ITouchStrip TouchStrip { get; }

	/// <summary>
	/// Starts the background HID read loop and connects to the device.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task ConnectAsync(CancellationToken cancellationToken);

	/// <inheritdoc cref="ConnectAsync(CancellationToken)"/>
	Task ConnectAsync() => ConnectAsync(CancellationToken.None);

	/// <summary>
	/// Stops the background read loop and releases the HID device.
	/// </summary>
	Task DisconnectAsync();

	/// <summary>
	/// Experimental: writes a simple top/bottom test pattern to the Mikro MK3 dot-matrix display.
	/// </summary>
	Task SetDotMatrixTestPatternAsync(CancellationToken cancellationToken);

	/// <inheritdoc cref="SetDotMatrixTestPatternAsync(CancellationToken)"/>
	Task SetDotMatrixTestPatternAsync() => SetDotMatrixTestPatternAsync(CancellationToken.None);

	/// <summary>
	/// Experimental: clears the Mikro MK3 dot-matrix display sections.
	/// </summary>
	Task ClearDotMatrixAsync(CancellationToken cancellationToken);

	/// <inheritdoc cref="ClearDotMatrixAsync(CancellationToken)"/>
	Task ClearDotMatrixAsync() => ClearDotMatrixAsync(CancellationToken.None);

	/// <summary>
	/// Experimental: writes a zebra-line pattern to the Mikro MK3 dot-matrix display.
	/// </summary>
	Task SetDotMatrixZebraLinesAsync(int phase, CancellationToken cancellationToken);

	/// <inheritdoc cref="SetDotMatrixZebraLinesAsync(int, CancellationToken)"/>
	Task SetDotMatrixZebraLinesAsync() => SetDotMatrixZebraLinesAsync(0, CancellationToken.None);

	/// <inheritdoc cref="SetDotMatrixZebraLinesAsync(int, CancellationToken)"/>
	Task SetDotMatrixZebraLinesAsync(CancellationToken cancellationToken) => SetDotMatrixZebraLinesAsync(0, cancellationToken);

	/// <inheritdoc cref="SetDotMatrixZebraLinesAsync(int, CancellationToken)"/>
	Task SetDotMatrixZebraLinesAsync(int phase) => SetDotMatrixZebraLinesAsync(phase, CancellationToken.None);

	/// <summary>
	/// Writes a raw monochrome bitmap to the Mikro MK3 dot-matrix display.
	/// </summary>
	/// <param name="bitmap">
	/// 512-byte packed row-major bitmap: 32 rows × 16 bytes/row.
	/// <c>bitmap[row * 16 + col / 8]</c> bit <c>7 − (col % 8)</c> is the pixel at
	/// <c>(row, col)</c>; a set bit = lit pixel.
	/// </param>
	/// <param name="xOffset">Signed pixel offset applied to the bitmap. Positive moves content right.</param>
	/// <param name="yOffset">Signed pixel offset applied to the bitmap. Positive moves content down.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetDotMatrixBitmapAsync(byte[] bitmap, int xOffset, int yOffset, CancellationToken cancellationToken);

	/// <inheritdoc cref="SetDotMatrixBitmapAsync(byte[], int, int, CancellationToken)"/>
	Task SetDotMatrixBitmapAsync(byte[] bitmap) => SetDotMatrixBitmapAsync(bitmap, 0, 0, CancellationToken.None);

	/// <inheritdoc cref="SetDotMatrixBitmapAsync(byte[], int, int, CancellationToken)"/>
	Task SetDotMatrixBitmapAsync(byte[] bitmap, CancellationToken cancellationToken) => SetDotMatrixBitmapAsync(bitmap, 0, 0, cancellationToken);

	/// <inheritdoc cref="SetDotMatrixBitmapAsync(byte[], int, int, CancellationToken)"/>
	Task SetDotMatrixBitmapAsync(byte[] bitmap, int xOffset, int yOffset) => SetDotMatrixBitmapAsync(bitmap, xOffset, yOffset, CancellationToken.None);

	/// <summary>
	/// Renders one or more text lines to the Mikro MK3 dot-matrix display.
	/// </summary>
	/// <param name="lines">
	/// Text lines to render. The number of lines consumed depends on <paramref name="mode"/>;
	/// extra lines are ignored and missing lines leave the corresponding band blank.
	/// </param>
	/// <param name="mode">
	/// Controls the number of rows, the font used, and any vertical scaling applied:
	/// <list type="table">
	///   <listheader><term>Mode</term><description>Layout</description></listheader>
	///   <item><term><see cref="DisplayLineMode.OneRow"/></term>
	///     <description>1 row, 8×8 font scaled 4× vertically (8×32 px glyphs, 16 chars).</description></item>
	///   <item><term><see cref="DisplayLineMode.TwoRows"/></term>
	///     <description>2 rows, 8×8 font scaled 2× vertically (8×16 px glyphs, 16 chars/row).</description></item>
	///   <item><term><see cref="DisplayLineMode.FourRows"/></term>
	///     <description>4 rows, standard 8×8 font (16 chars/row).</description></item>
	///   <item><term><see cref="DisplayLineMode.EightRows"/></term>
	///     <description>8 rows, compact 4×4 font (32 chars/row).</description></item>
	/// </list>
	/// Each mode accepts one extra hidden text row and one extra hidden character column,
	/// which can be revealed smoothly via the signed pixel offsets below.
	/// </param>
	/// <param name="xOffset">Signed pixel offset applied to the rendered text. Positive moves content right.</param>
	/// <param name="yOffset">Signed pixel offset applied to the rendered text. Positive moves content down.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SetDotMatrixTextAsync(IReadOnlyList<string> lines, DisplayLineMode mode, int xOffset, int yOffset, CancellationToken cancellationToken);

	/// <inheritdoc cref="SetDotMatrixTextAsync(IReadOnlyList{string}, DisplayLineMode, int, int, CancellationToken)"/>
	Task SetDotMatrixTextAsync(IReadOnlyList<string> lines, DisplayLineMode mode) => SetDotMatrixTextAsync(lines, mode, 0, 0, CancellationToken.None);

	/// <inheritdoc cref="SetDotMatrixTextAsync(IReadOnlyList{string}, DisplayLineMode, int, int, CancellationToken)"/>
	Task SetDotMatrixTextAsync(IReadOnlyList<string> lines, DisplayLineMode mode, CancellationToken cancellationToken) => SetDotMatrixTextAsync(lines, mode, 0, 0, cancellationToken);

	/// <inheritdoc cref="SetDotMatrixTextAsync(IReadOnlyList{string}, DisplayLineMode, int, int, CancellationToken)"/>
	Task SetDotMatrixTextAsync(IReadOnlyList<string> lines, DisplayLineMode mode, int xOffset, int yOffset) => SetDotMatrixTextAsync(lines, mode, xOffset, yOffset, CancellationToken.None);

	/// <summary>
	/// Renders a dashboard widget layout to the dot-matrix display (push mode).
	/// </summary>
	Task SetDotMatrixDashboardAsync(DotMatrixDashboard dashboard, CancellationToken cancellationToken);

	/// <inheritdoc cref="SetDotMatrixDashboardAsync(DotMatrixDashboard, CancellationToken)"/>
	Task SetDotMatrixDashboardAsync(DotMatrixDashboard dashboard) => SetDotMatrixDashboardAsync(dashboard, CancellationToken.None);

	/// <summary>
	/// Renders a list of widgets to the dot-matrix display (push mode).
	/// </summary>
	Task SetDotMatrixWidgetsAsync(IReadOnlyList<IDotMatrixWidget> widgets, CancellationToken cancellationToken);

	/// <inheritdoc cref="SetDotMatrixWidgetsAsync(IReadOnlyList{IDotMatrixWidget}, CancellationToken)"/>
	Task SetDotMatrixWidgetsAsync(IReadOnlyList<IDotMatrixWidget> widgets) => SetDotMatrixWidgetsAsync(widgets, CancellationToken.None);

	/// <summary>
	/// Continuously renders a dashboard at the requested frame rate until cancelled (loop mode).
	/// </summary>
	Task RunDotMatrixDashboardLoopAsync(DotMatrixDashboard dashboard, int framesPerSecond, CancellationToken cancellationToken);

	/// <inheritdoc cref="RunDotMatrixDashboardLoopAsync(DotMatrixDashboard, int, CancellationToken)"/>
	Task RunDotMatrixDashboardLoopAsync(DotMatrixDashboard dashboard) => RunDotMatrixDashboardLoopAsync(dashboard, 30, CancellationToken.None);

	/// <inheritdoc cref="RunDotMatrixDashboardLoopAsync(DotMatrixDashboard, int, CancellationToken)"/>
	Task RunDotMatrixDashboardLoopAsync(DotMatrixDashboard dashboard, CancellationToken cancellationToken) => RunDotMatrixDashboardLoopAsync(dashboard, 30, cancellationToken);

	/// <inheritdoc cref="RunDotMatrixDashboardLoopAsync(DotMatrixDashboard, int, CancellationToken)"/>
	Task RunDotMatrixDashboardLoopAsync(DotMatrixDashboard dashboard, int framesPerSecond) => RunDotMatrixDashboardLoopAsync(dashboard, framesPerSecond, CancellationToken.None);
}
