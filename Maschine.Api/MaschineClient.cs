using Maschine.Api.Exceptions;
using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maschine.Api;

/// <summary>
/// Main client for interacting with a connected Maschine Mikro MK3 controller.
/// </summary>
public sealed partial class MaschineClient : IMaschineClient
{
	private readonly MaschineClientOptions _options;
	private readonly IHidDeviceFactory _factory;
	private readonly ILogger<MaschineClient> _logger;
	private readonly LedBrightnessController _brightness;
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
	public Task ConnectAsync() => ConnectAsync(CancellationToken.None);

	/// <inheritdoc/>
	public Task ConnectAsync(CancellationToken cancellationToken)
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
		await BlankControllerAsync().ConfigureAwait(false);

		if (_readLoopCts is not null)
		{
			await _readLoopCts.CancelAsync().ConfigureAwait(false);
		}

		// Dispose the device before awaiting the read loop so a blocking HID read is interrupted.
		_device?.Dispose();
		_device = null;

		await WaitForReadLoopAsync().ConfigureAwait(false);
		ReleaseSubsystems();
		MaschineClientLog.Disconnected(_logger);
	}

	/// <summary>
	/// Best-effort visual cleanup so the controller is left dark on shutdown. Failures are ignored:
	/// the device may already be gone, and disconnect must complete regardless.
	/// </summary>
	private async Task BlankControllerAsync()
	{
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
	}

	private async Task WaitForReadLoopAsync()
	{
		if (_readLoop is null)
		{
			return;
		}

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

	private void ReleaseSubsystems()
	{
		_pads = null;
		_unifiedLights?.Dispose();
		_unifiedLights = null;
		_dotMatrixDisplay?.Dispose();
		_dotMatrixDisplay = null;
		_buttons = null;
		_encoders = null;
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

	private static T EnsureConnected<T>(T? value) where T : class
		=> value ?? throw new InvalidOperationException(
			"Not connected. Call ConnectAsync() before accessing device features.");
}
