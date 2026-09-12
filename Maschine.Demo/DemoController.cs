using Maschine.Api;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Maschine.Api.Widgets;
using Microsoft.Extensions.Logging;

namespace Maschine.Demo;

/// <summary>
/// Reactive demo for Maschine Mikro MK3.
///
/// Interactions:
///   Button press   → cycle that button LED through 3 brightness levels
///   Pad hit        → play a random colour/effect on that pad
///   Encoder turn   → logs movement and updates the strip LED meter
/// </summary>
internal sealed partial class DemoController : IAsyncDisposable
{
	private const int TouchStripEncoderIndex = 8;

	// ── Per-pad colour palette: one entry per pad, maps to all 16 device palette slots ────────

	private static readonly PadColor[] s_padColors =
	[
		new(255,   0,   0),   //  0  red           (h=  0°, palette  1)
		new(255,  72,   0),   //  1  orange         (h= 17°, palette  2)
		new(255, 132,   0),   //  2  light-orange   (h= 31°, palette  3)
		new(255, 191,   0),   //  3  warm-yellow    (h= 45°, palette  4)
		new(242, 255,   0),   //  4  yellow         (h= 63°, palette  5)
		new(128, 255,   0),   //  5  lime           (h= 90°, palette  6)
		new(  0, 255,   0),   //  6  green          (h=120°, palette  7)
		new(  0, 255, 128),   //  7  mint           (h=150°, palette  8)
		new(  0, 255, 255),   //  8  cyan           (h=180°, palette  9)
		new(  0, 128, 255),   //  9  turquoise      (h=210°, palette 10)
		new(  0,   0, 255),   // 10  blue           (h=240°, palette 11)
		new( 64,   0, 255),   // 11  plum           (h=255°, palette 12)
		new(128,   0, 255),   // 12  violet         (h=270°, palette 13)
		new(191,   0, 255),   // 13  purple         (h=285°, palette 14)
		new(255,   0, 255),   // 14  magenta        (h=300°, palette 15)
		new(255,   0, 119),   // 15  fuchsia        (h=332°, palette 16)
	];

	private static readonly PadColor[] s_touchStripDemoColors =
	[
		new(255,   0,   0),
		new(255,  48,   0),
		new(255,  96,   0),
		new(255, 144,   0),
		new(255, 192,   0),
		new(255, 224,   0),
		new(224, 255,   0),
		new(160, 255,   0),
		new( 96, 255,   0),
		new(  0, 255, 192),
		new(  0, 255, 255),
		new(  0, 208, 255),
		new(  0, 160, 255),
		new(  0, 112, 255),
		new(  0,  64, 255),
		new( 32,   0, 255),
		new( 80,   0, 255),
		new(128,   0, 255),
		new(160,   0, 255),
		new(192,   0, 255),
		new(224,   0, 255),
		new(255,   0, 224),
		new(255,   0, 176),
		new(255,   0, 128),
		new(255, 255, 255),
	];

	// ── Zebra speed table: delay in ms for |velocity| = 1..5 ────────────────

	private static readonly int[] s_zebraSpeedMs = [600, 200, 100, 50, 25];
	private static readonly TimeSpan s_padRetriggerDebounce = TimeSpan.FromMilliseconds(70);

	// ── Per-element state ───────────────────────────────────────────────────

	private readonly bool[] _padDown;
	private readonly DateTime[] _lastPadDownUtc;
	private readonly DateTime[] _lastEncoderLogUtc;
	private readonly SemaphoreSlim _touchStripUpdateGate = new(1, 1);
	private readonly object _animationSync = new();
	// -5..+5; sign=direction, |v|=speed (1=slow…5=fast). Fixed for now, but read through
	// GetDisplayVelocity so the animation loop has a single place to pick up a future control.
	private readonly int _displayVelocity = 3;
	private bool _isDashboardInverted;
	private readonly DotMatrixDashboard[] _dashboards;
	private int _audioFrame;
	private int _selectedDashboardIndex;
	private int _touchStripLevel;

	// The event signatures carry a sender this demo never uses. Subscribing through a lambda
	// discards it at the boundary, so no handler needs a parameter it ignores; the delegates are
	// stored because unsubscribing requires the same instances that were added.
	private EventHandler<KeyEvent>? _keyEventHandler;
	private EventHandler<EncoderTouchState>? _encoderTouchChangedHandler;
	private EventHandler<PadState>? _padChangedHandler;
	private EventHandler<EncoderDelta>? _encoderChangedHandler;

	private readonly IMaschineClient _client;
	private readonly ILogger<DemoController> _logger;
	private IButtons? _buttons;
	private DrumSoundfontPlayer? _drumPlayer;
	private IPads? _pads;
	private IEncoders? _encoders;
	private ITouchStrip? _touchStrip;
	private bool _subscribed;

	// ── Construction ────────────────────────────────────────────────────────

	internal DemoController(IMaschineClient client, ILogger<DemoController> logger)
	{
		_client = client;
		_logger = logger;

		_padDown = new bool[MaschineDeviceConstants.MikroMk3PadCount];
		_lastPadDownUtc = new DateTime[MaschineDeviceConstants.MikroMk3PadCount];
		_lastEncoderLogUtc = new DateTime[MaschineDeviceConstants.MikroMk3EncoderCount];
		_dashboards = BuildDashboards();
	}

	// ── Public API ──────────────────────────────────────────────────────────

	internal async Task RunAsync(DemoModes modes, CancellationToken cancellationToken)
	{
		PrintMappings();

		await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);

		_buttons = _client.Buttons;
		_pads = _client.Pads;
		_encoders = _client.Encoders;
		_touchStrip = _client.TouchStrip;
		_drumPlayer = await DrumSoundfontPlayer.CreateAsync(_logger, cancellationToken).ConfigureAwait(false);

		if (modes.IsInteractive)
		{
			Subscribe();
		}

		_logger.LogInformation("Device connected. Press Ctrl+C to exit.");

		await TryInitializeSurfaceAsync(cancellationToken).ConfigureAwait(false);
		await RunStartupModesAsync(modes, cancellationToken).ConfigureAwait(false);

		var displayTask = modes.DisplayShowcase ? RunDotMatrixShowcaseAsync(cancellationToken) : null;

		await WaitUntilCancelledAsync(cancellationToken).ConfigureAwait(false);
		await AwaitShowcaseAsync(displayTask).ConfigureAwait(false);

		Unsubscribe();
		await ShutdownSurfaceAsync().ConfigureAwait(false);

		_drumPlayer?.Dispose();
		_drumPlayer = null;

		await _client.DisconnectAsync().ConfigureAwait(false);
	}

	/// <summary>Applies the one-shot modes that run once, in order, before the demo idles.</summary>
	private async Task RunStartupModesAsync(DemoModes modes, CancellationToken cancellationToken)
	{
		if (modes.PadColorSpace)
		{
			await TrySetPadColorSpaceAsync(cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Pad color-space mode enabled (interactive mappings disabled).");
		}

		if (modes.LedSelfTest)
		{
			await RunLedSelfTestAsync(cancellationToken).ConfigureAwait(false);
		}

		if (modes.FullBrightness)
		{
			await TrySetAllLedsAsync(PadColor.White, 127, "full-brightness", cancellationToken)
				.ConfigureAwait(false);
			_logger.LogInformation("All pads/buttons set to full brightness (interactive mappings disabled).");
		}

		if (modes.DisplayTest)
		{
			await TrySetDotMatrixTestPatternAsync(cancellationToken).ConfigureAwait(false);
		}

		if (modes.DisplayZebra)
		{
			await TrySetDotMatrixZebraAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private void Subscribe()
	{
		if (_buttons is null || _pads is null || _encoders is null)
		{
			return;
		}

		_keyEventHandler = (_, e) => OnKeyEvent(e);
		_encoderTouchChangedHandler = (_, state) => OnEncoderTouchChanged(state);
		_padChangedHandler = (_, state) => OnPadChanged(state);
		_encoderChangedHandler = (_, delta) => OnEncoderChanged(delta);

		_buttons.KeyEvent += _keyEventHandler;
		_buttons.EncoderTouchChanged += _encoderTouchChangedHandler;
		_pads.PadChanged += _padChangedHandler;
		_encoders.EncoderChanged += _encoderChangedHandler;
		_subscribed = true;
	}

	private void Unsubscribe()
	{
		if (!_subscribed || _buttons is null || _pads is null || _encoders is null)
		{
			return;
		}

		_buttons.KeyEvent -= _keyEventHandler;
		_buttons.EncoderTouchChanged -= _encoderTouchChangedHandler;
		_pads.PadChanged -= _padChangedHandler;
		_encoders.EncoderChanged -= _encoderChangedHandler;
		_subscribed = false;
	}

	private static async Task WaitUntilCancelledAsync(CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Normal exit
		}
	}

	private static async Task AwaitShowcaseAsync(Task? displayTask)
	{
		if (displayTask is null)
		{
			return;
		}

		try
		{
			await displayTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Normal exit
		}
	}

	/// <summary>Blanks the surface on exit, while the device is still connected.</summary>
	private async Task ShutdownSurfaceAsync()
	{
		if (_pads is not null && _buttons is not null)
		{
			await TrySetAllLedsAsync(new PadColor(0, 0, 0), 0, "shutdown", CancellationToken.None).ConfigureAwait(false);
			await TryClearDotMatrixAsync(CancellationToken.None).ConfigureAwait(false);
		}

		if (_touchStrip is null)
		{
			return;
		}

		try
		{
			await _touchStrip.SetAllLedsAsync(0, CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "LED write failed during shutdown.");
		}
	}

	public async ValueTask DisposeAsync()
	{
		Unsubscribe();

		_touchStripUpdateGate.Dispose();
		_drumPlayer?.Dispose();
		_drumPlayer = null;

		await Task.CompletedTask.ConfigureAwait(false);
	}

	// ── Event handlers ──────────────────────────────────────────────────────
}
