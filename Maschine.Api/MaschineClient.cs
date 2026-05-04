using Maschine.Api.Exceptions;
using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using Maschine.Api.Widgets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;

namespace Maschine.Api;

// LoggerMessage delegates (CA1848 compliance)
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static partial class MaschineClientLog
{
	[LoggerMessage(Level = LogLevel.Information, Message = "Connected to Maschine device VID=0x{VendorId:X4} PID=0x{ProductId:X4}.")]
	public static partial void Connected(ILogger logger, int vendorId, int productId);

	[LoggerMessage(Level = LogLevel.Information, Message = "Disconnected from Maschine device.")]
	public static partial void Disconnected(ILogger logger);

	[LoggerMessage(Level = LogLevel.Error, Message = "Error reading from Maschine device.")]
	public static partial void ReadError(ILogger logger, Exception ex);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Unhandled report ID 0x{ReportId:X2}.")]
	public static partial void UnhandledReport(ILogger logger, byte reportId);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring malformed report ID 0x{ReportId:X2} (len={Length}).")]
	public static partial void MalformedReport(ILogger logger, byte reportId, int length);

	[LoggerMessage(Level = LogLevel.Debug, Message = "{Message}")]
	public static partial void TraceLine(ILogger logger, string message);
}

/// <summary>
/// Main client for interacting with a connected Maschine Mikro MK3 controller.
/// </summary>
public sealed class MaschineClient : IMaschineClient
{
	private readonly MaschineClientOptions _options;
	private readonly IHidDeviceFactory _factory;
	private readonly ILogger<MaschineClient> _logger;
	private readonly LedBrightnessController _brightness;
	private readonly Dictionary<byte, byte[]> _previousTracedReports = new();
	private readonly Dictionary<byte, DateTime> _previousTraceTimesById = new();
	private DateTime? _previousTraceTime;
	private IHidDevice? _device;
	private MaschinePads? _pads;
	private MaschineButtons? _buttons;
	private MaschineEncoders? _encoders;
	private MaschineTouchStrip? _touchStrip;
	private MikroMk3UnifiedLights? _unifiedLights;
	private MikroMk3DotMatrixDisplay? _dotMatrixDisplay;
	private CancellationTokenSource? _readLoopCts;
	private Task? _readLoop;
	private bool _disposed;

	/// <summary>
	/// Initialises a new <see cref="MaschineClient"/> with default options and the system HID factory.
	/// </summary>
	public MaschineClient()
		: this(new MaschineClientOptions(), new HidSharpDeviceFactory(), NullLogger<MaschineClient>.Instance)
	{
	}

	/// <summary>
	/// Initialises a new <see cref="MaschineClient"/> with the given options and the system HID factory.
	/// </summary>
	/// <param name="options">Options controlling device selection.</param>
	public MaschineClient(MaschineClientOptions options)
		: this(options, new HidSharpDeviceFactory(), NullLogger<MaschineClient>.Instance)
	{
	}

	/// <summary>
	/// Initialises a new <see cref="MaschineClient"/> with the given options, logger, and system HID factory.
	/// </summary>
	/// <param name="options">Options controlling device selection.</param>
	/// <param name="logger">Logger for diagnostic output.</param>
	public MaschineClient(MaschineClientOptions options, ILogger<MaschineClient> logger)
		: this(options, new HidSharpDeviceFactory(), logger)
	{
	}

	/// <summary>
	/// Initialises a new <see cref="MaschineClient"/> with an injected HID factory (for testing).
	/// </summary>
	/// <param name="options">Options controlling device selection.</param>
	/// <param name="factory">HID device factory.</param>
	/// <param name="logger">Logger for diagnostic output.</param>
	internal MaschineClient(MaschineClientOptions options, IHidDeviceFactory factory, ILogger<MaschineClient> logger)
	{
		_options = options;
		_factory = factory;
		_logger = logger;
		_brightness = new LedBrightnessController(options.GlobalLedBrightnessPercent);
	}

	/// <inheritdoc/>
	public int LedBrightnessPercent
	{
		get => _brightness.Percent;
		set => _brightness.Percent = value;
	}

	/// <inheritdoc/>
	public IPads Pads => EnsureConnected(_pads);

	/// <inheritdoc/>
	public IButtons Buttons => EnsureConnected(_buttons);

	/// <inheritdoc/>
	public IEncoders Encoders => EnsureConnected(_encoders);

	/// <inheritdoc/>
	public ITouchStrip TouchStrip => EnsureConnected(_touchStrip);

	/// <inheritdoc/>
	public Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		_device = _factory.TryOpen(_options.VendorId, _options.ProductId, _options.DeviceIndex)
			?? throw new MaschineDeviceNotFoundException(_options.VendorId, _options.ProductId);

		_unifiedLights = new MikroMk3UnifiedLights(_device);
		if (_options.ForceUnifiedLightOutput)
		{
			_unifiedLights.Enable();
		}

		_pads = new MaschinePads(_device, _unifiedLights);
		_buttons = new MaschineButtons(_device, _unifiedLights, _brightness, _options);
		_touchStrip = new MaschineTouchStrip(_unifiedLights, _brightness);
		_dotMatrixDisplay = new MikroMk3DotMatrixDisplay(_device);
		_encoders = new MaschineEncoders();

		_readLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_readLoop = RunReadLoopAsync(_readLoopCts.Token);

		MaschineClientLog.Connected(_logger, _options.VendorId, _options.ProductId);

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public async Task DisconnectAsync()
	{
		// Best-effort visual cleanup so the controller is left dark on shutdown.
		try
		{
			if (_pads is not null)
			{
				await _pads.SetAllColorsAsync(PadColor.Off, CancellationToken.None).ConfigureAwait(false);
			}

			if (_buttons is not null)
			{
				await _buttons.SetAllLedsForShutdownAsync(0, CancellationToken.None).ConfigureAwait(false);
			}

			if (_touchStrip is not null)
			{
				await _touchStrip.SetAllLedsAsync(0, CancellationToken.None).ConfigureAwait(false);
			}

			if (_dotMatrixDisplay is not null)
			{
				await _dotMatrixDisplay.ClearWithFallbackAsync(CancellationToken.None).ConfigureAwait(false);
			}
		}
		catch
		{
			// Ignore cleanup failures during disconnect.
		}

		if (_readLoopCts is not null)
		{
			await _readLoopCts.CancelAsync().ConfigureAwait(false);
		}

		// Dispose the device before awaiting the read loop so a blocking HID read is interrupted.
		_device?.Dispose();
		_device = null;

		if (_readLoop is not null)
		{
			try
			{
				await _readLoop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected on disconnect.
			}
			catch (TimeoutException)
			{
				// Some HID backends can ignore cancellation while blocked; continue shutdown.
			}
		}

		_pads = null;
		_unifiedLights?.Dispose();
		_unifiedLights = null;
		_dotMatrixDisplay?.Dispose();
		_dotMatrixDisplay = null;
		_buttons = null;
		_encoders = null;
		MaschineClientLog.Disconnected(_logger);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_readLoopCts?.Cancel();
		_readLoopCts?.Dispose();
		_device?.Dispose();
		_unifiedLights?.Dispose();
		_dotMatrixDisplay?.Dispose();
	}

	/// <inheritdoc/>
	public Task SetDotMatrixTestPatternAsync(CancellationToken cancellationToken = default)
		=> EnsureConnected(_dotMatrixDisplay).SetTestPatternAsync(cancellationToken);

	/// <inheritdoc/>
	public Task ClearDotMatrixAsync(CancellationToken cancellationToken = default)
		=> EnsureConnected(_dotMatrixDisplay).ClearAsync(cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixZebraLinesAsync(int phase = 0, CancellationToken cancellationToken = default)
		=> EnsureConnected(_dotMatrixDisplay).SetZebraLinesAsync(phase, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixBitmapAsync(byte[] bitmap, int xOffset = 0, int yOffset = 0, CancellationToken cancellationToken = default)
		=> EnsureConnected(_dotMatrixDisplay).SetBitmapAsync(bitmap, xOffset, yOffset, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixTextAsync(IReadOnlyList<string> lines, DisplayLineMode mode, int xOffset = 0, int yOffset = 0, CancellationToken cancellationToken = default)
		=> EnsureConnected(_dotMatrixDisplay).SetTextAsync(lines, mode, xOffset, yOffset, cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixDashboardAsync(DotMatrixDashboard dashboard, CancellationToken cancellationToken = default)
		=> SetDotMatrixBitmapAsync((dashboard ?? throw new ArgumentNullException(nameof(dashboard))).BuildBitmap(), cancellationToken: cancellationToken);

	/// <inheritdoc/>
	public Task SetDotMatrixWidgetsAsync(IReadOnlyList<IDotMatrixWidget> widgets, CancellationToken cancellationToken = default)
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
	public async Task RunDotMatrixDashboardLoopAsync(DotMatrixDashboard dashboard, int framesPerSecond = 30, CancellationToken cancellationToken = default)
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
				await SetDotMatrixBitmapAsync(frame, cancellationToken: cancellationToken).ConfigureAwait(false);
				previousFrame = frame;
			}

			dashboard.AdvanceFrame();
			await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
		}
	}

	// Private

	private async Task RunReadLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				var report = await _device!.ReadAsync(cancellationToken).ConfigureAwait(false);
				if (report.Length == 0)
				{
					continue;
				}

				DispatchReport(report);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				MaschineClientLog.ReadError(_logger, ex);
				break;
			}
		}
	}

	[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	private void DispatchReport(byte[] report)
	{
		if (report.Length == 0)
		{
			return;
		}

		try
		{
			if (_options.TraceInputReports && _logger.IsEnabled(LogLevel.Debug))
			{
				var kind = report[0] switch
				{
					MikroMk3Protocol.PadPressureReportId => "PAD",
					MikroMk3Protocol.ButtonReportId => "BUTTON",
					_ => "UNKNOWN",
				};
				var now = DateTime.UtcNow;
				var timestamp = now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
				var dtAll = _previousTraceTime.HasValue
					? $"dtAll={((now - _previousTraceTime.Value).TotalMilliseconds):0.0}ms"
					: "dtAll=init";
				var dtId = _previousTraceTimesById.TryGetValue(report[0], out var previousForId)
					? $"dtId={((now - previousForId).TotalMilliseconds):0.0}ms"
					: "dtId=init";
				_previousTraceTime = now;
				_previousTraceTimesById[report[0]] = now;

				var hex = BitConverter.ToString(report).Replace('-', ' ');
				var diff = BuildTraceDiff(report);
				var head = report.Length >= 4
					? $"head=[{report[1]:X2} {report[2]:X2} {report[3]:X2}] "
					: string.Empty;
#pragma warning disable CA1873
				MaschineClientLog.TraceLine(_logger, $"[{timestamp}Z] Input {kind} ID=0x{report[0]:X2} len={report.Length} {dtAll} {dtId} {diff} {head}bytes=[{hex}]");
#pragma warning restore CA1873
			}

			switch (report[0])
			{
				case MikroMk3Protocol.PadPressureReportId:
					_pads?.ApplyReport(report);
					break;

				case MikroMk3Protocol.ButtonReportId:
					_buttons?.ApplyReport(report);
					_encoders?.ApplyTouchStripButtonReport(report);
					break;

				default:
					MaschineClientLog.UnhandledReport(_logger, report[0]);
					break;
			}
		}
		catch (ArgumentException)
		{
			MaschineClientLog.MalformedReport(_logger, report[0], report.Length);
		}
	}

	private string BuildTraceDiff(byte[] report)
	{
		if (!_previousTracedReports.TryGetValue(report[0], out var previous))
		{
			_previousTracedReports[report[0]] = (byte[])report.Clone();
			return "chg=init";
		}

		var max = Math.Min(previous.Length, report.Length);
		var changes = new List<string>();
		for (var i = 0; i < max; i++)
		{
			if (previous[i] != report[i])
			{
				changes.Add($"{i}:{previous[i]:X2}->{report[i]:X2}");
				if (changes.Count >= 10)
				{
					break;
				}
			}
		}

		if (previous.Length != report.Length)
		{
			changes.Add($"len:{previous.Length}->{report.Length}");
		}

		_previousTracedReports[report[0]] = (byte[])report.Clone();
		return changes.Count == 0 ? "chg=none" : $"chg=[{string.Join(',', changes)}]";
	}

	private static T EnsureConnected<T>(T? value) where T : class
		=> value ?? throw new InvalidOperationException(
			"Not connected. Call ConnectAsync() before accessing device features.");
}
