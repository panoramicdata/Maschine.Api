using Maschine.Api;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Maschine.Api.Widgets;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Maschine.Demo;

/// <summary>
/// Reactive demo for Maschine Mikro MK3.
///
/// Interactions:
///   Button press   → cycle that button LED through 3 brightness levels
///   Pad hit        → play a random colour/effect on that pad
///   Encoder turn   → logs movement and updates the strip LED meter
/// </summary>
internal sealed class DemoController : IAsyncDisposable
{
	private static readonly byte[] s_buttonBrightnessCycle = [0, 64, 127, 255];
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

	// ── Per-element state ───────────────────────────────────────────────────

	private readonly byte[] _buttonBrightness;
	private readonly bool[] _padDown;
	private readonly DateTime[] _lastEncoderLogUtc;
	private readonly SemaphoreSlim _touchStripUpdateGate = new(1, 1);
	private readonly object _animationSync = new();
	private int _displayVelocity = 3;         // -5..+5; sign=direction, |v|=speed (1=slow…5=fast)
	private bool _isDashboardInverted;
	private readonly DotMatrixDashboard[] _dashboards;
	private int _audioFrame;
	private int _selectedDashboardIndex;
	private int _touchStripLevel;
	private int _touchStripRenderedLevel = -1;

	private readonly IMaschineClient _client;
	private readonly ILogger<DemoController> _logger;
	private IButtons? _buttons;
	private IPads? _pads;
	private IEncoders? _encoders;
	private ITouchStrip? _touchStrip;
	private bool _subscribed;

	// ── Construction ────────────────────────────────────────────────────────

	internal DemoController(IMaschineClient client, ILogger<DemoController> logger)
	{
		_client = client;
		_logger = logger;

		_buttonBrightness = new byte[MaschineDeviceConstants.MikroMk3ButtonCount];
		_padDown = new bool[MaschineDeviceConstants.MikroMk3PadCount];
		_lastEncoderLogUtc = new DateTime[MaschineDeviceConstants.MikroMk3EncoderCount];
		_dashboards = BuildDashboards();
	}

	// ── Public API ──────────────────────────────────────────────────────────

	internal async Task RunAsync(
		CancellationToken cancellationToken,
		bool runLedSelfTest = false,
		bool runFullBrightness = false,
		bool runPadColorSpace = false,
		bool runDisplayTest = false,
		bool runDisplayZebra = false,
		bool runDisplayShowcase = false)
	{
		PrintMappings();

		await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);

		_buttons = _client.Buttons;
		_pads = _client.Pads;
		_encoders = _client.Encoders;
		_touchStrip = _client.TouchStrip;

		if (!runFullBrightness && !runPadColorSpace)
		{
			_buttons.ButtonChanged += OnButtonChanged;
			_buttons.ButtonPressed += OnButtonPressed;
			_buttons.ButtonReleased += OnButtonReleased;
			_buttons.EncoderTouchChanged += OnEncoderTouchChanged;
			_pads.PadChanged += OnPadChanged;
			_encoders.EncoderChanged += OnEncoderChanged;
			_subscribed = true;
		}

		_logger.LogInformation("Device connected. Press Ctrl+C to exit.");

		await TryInitializeSurfaceAsync(cancellationToken).ConfigureAwait(false);

		if (runPadColorSpace)
		{
			await TrySetPadColorSpaceAsync(cancellationToken).ConfigureAwait(false);
		}

		if (runLedSelfTest)
		{
			await RunLedSelfTestAsync(cancellationToken).ConfigureAwait(false);
		}

		if (runFullBrightness)
		{
			await TrySetAllLedsAsync(PadColor.White, 127, "full-brightness", cancellationToken)
				.ConfigureAwait(false);
			_logger.LogInformation("All pads/buttons set to full brightness (interactive mappings disabled).");
		}

		if (runPadColorSpace)
		{
			_logger.LogInformation("Pad color-space mode enabled (interactive mappings disabled).");
		}

		if (runDisplayTest)
		{
			await TrySetDotMatrixTestPatternAsync(cancellationToken).ConfigureAwait(false);
		}

		if (runDisplayZebra)
		{
			await TrySetDotMatrixZebraAsync(cancellationToken).ConfigureAwait(false);
		}

		Task? displayTask = null;
		if (runDisplayShowcase)
		{
			displayTask = RunDotMatrixShowcaseAsync(cancellationToken);
		}

		try
		{
			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Normal exit
		}

		if (displayTask is not null)
		{
			try
			{
				await displayTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Normal exit
			}
		}

		if (_subscribed && _buttons is not null && _pads is not null && _encoders is not null)
		{
			_buttons.ButtonChanged -= OnButtonChanged;
			_buttons.ButtonPressed -= OnButtonPressed;
			_buttons.ButtonReleased -= OnButtonReleased;
			_buttons.EncoderTouchChanged -= OnEncoderTouchChanged;
			_pads.PadChanged -= OnPadChanged;
			_encoders.EncoderChanged -= OnEncoderChanged;
			_subscribed = false;
		}

		// Blank on exit while still connected
		if (_pads is not null && _buttons is not null)
		{
			await TrySetAllLedsAsync(new PadColor(0, 0, 0), 0, "shutdown", CancellationToken.None).ConfigureAwait(false);
			await TryClearDotMatrixAsync(CancellationToken.None).ConfigureAwait(false);
		}

		if (_touchStrip is not null)
		{
			try
			{
				await _touchStrip.SetAllLedsAsync(0, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "LED write failed during shutdown.");
			}
		}

		await _client.DisconnectAsync().ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		if (_subscribed && _buttons is not null && _pads is not null && _encoders is not null)
		{
			_buttons.ButtonChanged -= OnButtonChanged;
			_buttons.ButtonPressed -= OnButtonPressed;
			_buttons.ButtonReleased -= OnButtonReleased;
			_buttons.EncoderTouchChanged -= OnEncoderTouchChanged;
			_pads.PadChanged -= OnPadChanged;
			_encoders.EncoderChanged -= OnEncoderChanged;
			_subscribed = false;
		}

		_touchStripUpdateGate.Dispose();

		await Task.CompletedTask.ConfigureAwait(false);
	}

	// ── Event handlers ──────────────────────────────────────────────────────

	private void OnEncoderTouchChanged(object? sender, EncoderTouchState state)
	{
		var dashboardIndex = state.KnobValue % _dashboards.Length;
		var changed = false;
		lock (_animationSync)
		{
			if (_selectedDashboardIndex != dashboardIndex)
			{
				_selectedDashboardIndex = dashboardIndex;
				changed = true;
			}
		}

		var touchStr = state.IsTouched ? "touched" : "released";
		_logger.LogInformation("Knob {TouchState}, position={KnobValue}, Dashboard={DashboardIndex}", touchStr, state.KnobValue, dashboardIndex);
		if (changed)
		{
			_logger.LogInformation("Active Dashboard -> {DashboardIndex}: {DashboardTitle}", dashboardIndex, GetDashboardTitle(dashboardIndex));
		}
	}

	private void OnButtonChanged(object? sender, Maschine.Api.Models.ButtonState state)
	{
		if (!state.IsPressed)
		{
			return; // act on press only
		}

		var buttonDescriptor = FormatButtonState(state);

		if (state.Index == (int)MikroMk3Button.MachineLogo)
		{
			bool inverted;
			lock (_animationSync)
			{
				_isDashboardInverted = !_isDashboardInverted;
				inverted = _isDashboardInverted;
			}

			_logger.LogInformation("Button action: {ButtonDescriptor} -> Dashboard invert {InvertState}", buttonDescriptor, inverted ? "ON" : "OFF");
			_ = TrySetButtonLedAsync(state.Index, inverted ? (byte)127 : (byte)0);
			return;
		}

		byte nextBrightness;
		lock (_animationSync)
		{
			var current = _buttonBrightness[state.Index];
			var currentIndex = Array.IndexOf(s_buttonBrightnessCycle, current);
			var nextIndex = (currentIndex + 1) % s_buttonBrightnessCycle.Length;
			nextBrightness = s_buttonBrightnessCycle[nextIndex];
			_buttonBrightness[state.Index] = nextBrightness;
		}

		_logger.LogInformation("Button action: {ButtonDescriptor} -> brightness {Brightness}", buttonDescriptor, nextBrightness);
		_ = TrySetButtonLedAsync(state.Index, nextBrightness);
	}

	private void OnButtonPressed(object? sender, Maschine.Api.Models.ButtonState state)
		=> _logger.LogInformation("Button DOWN: {ButtonState}", FormatButtonState(state));

	private void OnButtonReleased(object? sender, Maschine.Api.Models.ButtonState state)
		=> _logger.LogInformation("Button UP:   {ButtonState}", FormatButtonState(state));

	private void OnPadChanged(object? sender, PadState state)
	{
		const int PressThreshold = 220;
		const int ReleaseThreshold = 80;

		var restoreColor = GetPadBaseColor(state.Index);
		var wasDown = false;
		var isDown = false;

		lock (_animationSync)
		{
			wasDown = _padDown[state.Index];
			if (wasDown)
			{
				isDown = state.Pressure > ReleaseThreshold;
				_padDown[state.Index] = isDown;
			}
			else
			{
				isDown = state.Pressure >= PressThreshold;
				_padDown[state.Index] = isDown;
			}
		}

		if (!wasDown && isDown)
		{
			_logger.LogInformation("Pad DOWN: P{PadNumber,2} (raw {PadRaw,2}), pressure={Pressure} -> white", ToUserPadNumber(state.Index), state.Index, state.Pressure);
			_ = TrySetPadColorAsync(state.Index, PadColor.White);
			return;
		}

		if (wasDown && !isDown)
		{
			_logger.LogInformation("Pad UP:   P{PadNumber,2} (raw {PadRaw,2}), pressure={Pressure} -> {Color}", ToUserPadNumber(state.Index), state.Index, state.Pressure, FormatColor(restoreColor));
			_ = TrySetPadColorAsync(state.Index, restoreColor);
			return;
		}
	}

	private void OnEncoderChanged(object? sender, EncoderDelta delta)
	{
		const int TouchStripNoiseFloor = 8;
		const int EncoderNoiseFloor = 24;
		const int LogThrottleMs = 60;

		var noiseFloor = delta.Index == TouchStripEncoderIndex ? TouchStripNoiseFloor : EncoderNoiseFloor;
		if (Math.Abs(delta.Delta) < noiseFloor)
		{
			return;
		}

		var step = Math.Sign(delta.Delta);
		if (step == 0)
		{
			return;
		}

		if (delta.Index == TouchStripEncoderIndex)
		{
			// Calibrated axis for strip LEDs.
			var nowUtc = DateTime.UtcNow;
			bool shouldLog;
			lock (_animationSync)
			{
				shouldLog = (nowUtc - _lastEncoderLogUtc[delta.Index]).TotalMilliseconds >= LogThrottleMs;
				if (shouldLog)
				{
					_lastEncoderLogUtc[delta.Index] = nowUtc;
				}

				_touchStripLevel = Math.Clamp(_touchStripLevel + step, 0, MaschineDeviceConstants.MikroMk3TouchStripLedCount);
			}

			if (shouldLog)
			{
				_logger.LogInformation("Slider at position {Position}", _touchStripLevel);
				_logger.LogDebug("Slider delta {Delta:+#;-#;0} (encoder index {Index})", delta.Delta, delta.Index);
			}

			_ = UpdateTouchStripLedsCoalescedAsync();
		}
		else
		{
			// Ignore auxiliary encoder noise in the demo; it obscures pad/button logs.
		}
	}

	private async Task UpdateTouchStripLedsCoalescedAsync()
	{
		if (_touchStrip is null)
		{
			return;
		}

		if (!await _touchStripUpdateGate.WaitAsync(0).ConfigureAwait(false))
		{
			return;
		}

		try
		{
			while (true)
			{
				int level;
				lock (_animationSync)
				{
					level = _touchStripLevel;
				}

				if (level != _touchStripRenderedLevel)
				{
					var leds = new PadColor[MaschineDeviceConstants.MikroMk3TouchStripLedCount];
					var activeColor = level == 0 ? PadColor.Off : s_touchStripDemoColors[level - 1];
					for (var i = 0; i < leds.Length; i++)
					{
						leds[i] = i < level ? activeColor : PadColor.Off;
					}

					await _touchStrip.SetLedsAsync(leds).ConfigureAwait(false);
					_touchStripRenderedLevel = level;
				}

				int latest;
				lock (_animationSync)
				{
					latest = _touchStripLevel;
				}

				if (latest == _touchStripRenderedLevel)
				{
					break;
				}
			}
		}
		finally
		{
			_touchStripUpdateGate.Release();
		}
	}



	private async Task TryInitializeSurfaceAsync(CancellationToken cancellationToken)
	{
		if (_pads is null || _buttons is null)
		{
			return;
		}

		try
		{
			// Write button state first. If button writes force unified-light fallback,
			// subsequent pad writes populate unified pad slots instead of being cleared.
			await _buttons.SetAllLedsAsync(0, cancellationToken).ConfigureAwait(false);

			if (_touchStrip is not null)
			{
				await _touchStrip.SetAllLedsAsync(0, cancellationToken).ConfigureAwait(false);
			}

			for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
			{
				await _pads.SetColorAsync(pad, GetPadBaseColor(pad), cancellationToken).ConfigureAwait(false);
			}
			_touchStripLevel = 13;
			_touchStripRenderedLevel = -1;
			Array.Fill(_buttonBrightness, (byte)0);
			await UpdateTouchStripLedsCoalescedAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "LED write failed during startup.");
		}
	}

	private async Task TrySetAllLedsAsync(PadColor padColor, byte buttonBrightness, string phase, CancellationToken cancellationToken)
	{
		if (_pads is null || _buttons is null)
		{
			return;
		}

		try
		{
			await _buttons.SetAllLedsAsync(buttonBrightness, cancellationToken).ConfigureAwait(false);
			await _pads.SetAllColorsAsync(padColor, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "LED write failed during {Phase}.", phase);
		}
	}

	private async Task TrySetPadColorAsync(int padIndex, PadColor color)
	{
		if (_pads is null)
		{
			return;
		}

		try
		{
			await _pads.SetColorAsync(MapPadIndexWithVerticalFlip(padIndex), color).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Pad write failed for P{PadIndex}.", padIndex);
		}
	}

	private async Task TrySetButtonLedAsync(int buttonIndex, byte brightness)
	{
		if (_buttons is null)
		{
			return;
		}

		try
		{
			await _buttons.SetLedAsync(buttonIndex, brightness).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Button write failed for B{ButtonIndex}.", buttonIndex);
		}
	}

	private async Task TrySetPadColorSpaceAsync(CancellationToken cancellationToken)
	{
		if (_pads is null)
		{
			return;
		}

		try
		{
			for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
			{
				var color = GetPadBaseColor(pad);
				await _pads.SetColorAsync(pad, color, cancellationToken).ConfigureAwait(false);
			}

			_logger.LogInformation("Pad color-space written across all 16 pads:");
			for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
			{
				var color = GetPadBaseColor(pad);
				_logger.LogInformation("  P{Pad,2} -> {Color}", pad, FormatColor(color));
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Pad color-space write failed.");
		}
	}

	private async Task TrySetDotMatrixTestPatternAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.SetDotMatrixTestPatternAsync(cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Dot-matrix test pattern written.");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Dot-matrix write failed.");
		}
	}

	private async Task TrySetDotMatrixZebraAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.SetDotMatrixZebraLinesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Dot-matrix zebra pattern written.");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Dot-matrix zebra write failed.");
		}
	}

	private async Task RunDotMatrixShowcaseAsync(CancellationToken cancellationToken)
	{
			_logger.LogInformation("Dashboard demo started. Knob position selects the active Dashboard.");
		byte[]? previousFrame = null;
		while (!cancellationToken.IsCancellationRequested)
		{
			DotMatrixDashboard dashboard;
			var invert = false;
			var frameNumber = 0;
			lock (_animationSync)
			{
				dashboard = _dashboards[_selectedDashboardIndex];
				invert = _isDashboardInverted;
				frameNumber = _audioFrame++;
			}

			UpdateFakeAudioWidgets(dashboard, frameNumber);

			var frame = dashboard.BuildBitmap();
			if (invert)
			{
				InvertBitmap(frame);
			}

			if (previousFrame is null || !frame.AsSpan().SequenceEqual(previousFrame))
			{
				await _client.SetDotMatrixBitmapAsync(frame, cancellationToken: cancellationToken).ConfigureAwait(false);
				previousFrame = frame;
			}

			var signedVelocity = GetDisplayVelocity();
			dashboard.AdvanceFrame(Math.Sign(signedVelocity));

			var velocity = Math.Abs(signedVelocity);
			var delayMs = velocity == 0 ? 100 : s_zebraSpeedMs[Math.Clamp(velocity, 1, s_zebraSpeedMs.Length) - 1];
			await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
		}
	}

	private int GetDisplayVelocity()
	{
		lock (_animationSync)
		{
			return _displayVelocity;
		}
	}

	private static string GetDashboardTitle(int index) => index switch
	{
		0 => "Overview",
		1 => "Status",
		2 => "Mix A",
		3 => "Mix B",
		4 => "Levels",
		5 => "Spectrum",
		6 => "Needle",
		7 => "Mini",
		8 => "Large",
		_ => $"Dashboard {index}",
	};

	private static DotMatrixDashboard[] BuildDashboards()
	{
		return
		[
			BuildOverviewDashboard(),
			BuildStatusDashboard(),
			BuildMixDashboard("A", 1),
			BuildMixDashboard("B", 2),
			BuildLevelsDashboard(),
			BuildSpectrumDashboard(),
			BuildNeedleDashboard(),
			BuildMiniDashboard(),
			BuildLargeDashboard(),
		];
	}

	private static DotMatrixDashboard BuildOverviewDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 8), ["Dashboard Overview"], TextOverflowMode.Ellipsis)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new TextWidget("sub", new DisplayZone(0, 8, 128, 8), ["Knob picks dashboard"], TextOverflowMode.Scroll)
		{
			OverflowStepPixels = 1,
			ScrollPadding = 4,
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new SpectrumWidget("eq", new DisplayZone(0, 16, 64, 16), [0.1f, 0.5f, 0.8f, 0.4f, 0.6f, 0.2f, 0.9f, 0.3f])
		{
			GapPixels = 0,
			ShowPeakMarkers = true,
			PeakHoldFrames = 10,
			PeakDecayPerFrame = 0.02f,
			ResponseRise = 0.65f,
			ResponseFall = 0.2f,
		});
		dashboard.AddWidget(new VuWidget("vu", new DisplayZone(64, 16, 64, 16), VuWidgetStyle.Bar, level: 0.6f, peakLevel: 0.8f)
		{
			PeakHoldFrames = 10,
			PeakDecayPerFrame = 0.02f,
			ResponseRise = 0.65f,
			ResponseFall = 0.2f,
			ShowPeakMarker = true,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildStatusDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("header", new DisplayZone(0, 0, 128, 16), ["Status", "Buttons Pads Encoders"], TextOverflowMode.Ellipsis)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new TextWidget("ticker", new DisplayZone(0, 16, 128, 16), ["Widgets render inside Dashboards with no overlap allowed"], TextOverflowMode.Scroll)
		{
			OverflowStepPixels = 2,
			ScrollPadding = 5,
			FontKind = TextFontKind.Proportional12,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildMixDashboard(string suffix, int variant)
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 64, 8), [$"Mix {suffix}"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new VuWidget("vuL", new DisplayZone(0, 8, 20, 24), VuWidgetStyle.Bar, level: 0.2f * variant + 0.2f, peakLevel: 0.85f)
		{
			PeakHoldFrames = 6,
			PeakDecayPerFrame = 0.03f,
		});
		dashboard.AddWidget(new VuWidget("vuR", new DisplayZone(22, 8, 20, 24), VuWidgetStyle.Bar, level: 0.3f * variant + 0.1f, peakLevel: 0.9f, invert: true)
		{
			PeakHoldFrames = 6,
			PeakDecayPerFrame = 0.03f,
		});
		dashboard.AddWidget(new SpectrumWidget("eq", new DisplayZone(48, 8, 80, 24), [0.15f, 0.30f, 0.60f, 0.75f, 0.50f, 0.40f, 0.70f, 0.95f])
		{
			GapPixels = 1,
			PeakHoldFrames = 8,
			PeakDecayPerFrame = 0.03f,
			ResponseRise = 0.6f,
			ResponseFall = 0.2f,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildLevelsDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 8), ["Levels"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new VuWidget("top", new DisplayZone(0, 8, 128, 8), VuWidgetStyle.Bar, level: 0.35f, peakLevel: 0.5f));
		dashboard.AddWidget(new VuWidget("mid", new DisplayZone(0, 16, 128, 8), VuWidgetStyle.Bar, level: 0.65f, peakLevel: 0.8f, invert: true));
		dashboard.AddWidget(new VuWidget("low", new DisplayZone(0, 24, 128, 8), VuWidgetStyle.Bar, level: 0.9f, peakLevel: 1.0f));
		return dashboard;
	}

	private static DotMatrixDashboard BuildSpectrumDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 8), ["Spectrum"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new SpectrumWidget("bands", new DisplayZone(0, 8, 128, 24), [0.05f, 0.12f, 0.20f, 0.35f, 0.50f, 0.75f, 0.95f, 0.85f, 0.65f, 0.45f, 0.30f, 0.18f, 0.10f, 0.06f])
		{
			GapPixels = 1,
			ShowPeakMarkers = true,
			PeakHoldFrames = 14,
			PeakDecayPerFrame = 0.015f,
			ResponseRise = 0.7f,
			ResponseFall = 0.15f,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildNeedleDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 8), ["VU Needle Widgets"], TextOverflowMode.Ellipsis)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new VuWidget("left", new DisplayZone(0, 8, 64, 24), VuWidgetStyle.Needle, VuNeedleDetailMode.Detailed, level: 0.3f, peakLevel: 0.45f)
		{
			NeedleStartDegrees = -70,
			NeedleSweepDegrees = 140,
			PeakHoldFrames = 10,
			PeakDecayPerFrame = 0.02f,
		});
		dashboard.AddWidget(new VuWidget("right", new DisplayZone(64, 8, 64, 24), VuWidgetStyle.Needle, VuNeedleDetailMode.Simple, level: 0.75f, peakLevel: 0.85f, invert: true)
		{
			NeedleStartDegrees = -55,
			NeedleSweepDegrees = 110,
			PeakHoldFrames = 6,
			PeakDecayPerFrame = 0.04f,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildMiniDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("row1", new DisplayZone(0, 0, 128, 4), ["mini dashboard widget row 1 rotates"], TextOverflowMode.Rotate) { OverflowStepPixels = 1, FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row2", new DisplayZone(0, 4, 128, 4), ["row 2 scrolls with spaces"], TextOverflowMode.Scroll) { OverflowStepPixels = 1, ScrollPadding = 6, FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row3", new DisplayZone(0, 8, 128, 4), ["row 3"], TextOverflowMode.None) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row4", new DisplayZone(0, 12, 128, 4), ["ellipsized widgets still fit"], TextOverflowMode.Ellipsis) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row5", new DisplayZone(0, 16, 128, 4), ["dashboard 7"], TextOverflowMode.None) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row6", new DisplayZone(0, 20, 128, 4), ["widget layout ok"], TextOverflowMode.None) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row7", new DisplayZone(0, 24, 128, 4), ["no overlap allowed"], TextOverflowMode.None) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row8", new DisplayZone(0, 28, 128, 4), ["knob = dashboard"], TextOverflowMode.Ellipsis) { FontKind = TextFontKind.Proportional4 });
		return dashboard;
	}

	private static DotMatrixDashboard BuildLargeDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 12), ["12px Bold Helvetica"], TextOverflowMode.Ellipsis)
		{
			FontKind = TextFontKind.Proportional12Bold,
		});
		dashboard.AddWidget(new TextWidget("sub", new DisplayZone(0, 12, 128, 12), ["Proportional 12 regular scrolls across the display"], TextOverflowMode.Scroll)
		{
			OverflowStepPixels = 1,
			ScrollPadding = 4,
			FontKind = TextFontKind.Proportional12,
		});
		dashboard.AddWidget(new VuWidget("vuL", new DisplayZone(0, 24, 62, 8), VuWidgetStyle.Bar, level: 0.6f, peakLevel: 0.75f)
		{
			PeakHoldFrames = 8,
			PeakDecayPerFrame = 0.025f,
		});
		dashboard.AddWidget(new VuWidget("vuR", new DisplayZone(66, 24, 62, 8), VuWidgetStyle.Bar, level: 0.4f, peakLevel: 0.55f, invert: true)
		{
			PeakHoldFrames = 8,
			PeakDecayPerFrame = 0.025f,
		});
		return dashboard;
	}

	private async Task TryClearDotMatrixAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.ClearDotMatrixAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			// Best-effort cleanup.
		}
	}

	private async Task RunLedSelfTestAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Running LED self-test: global colors, pad chase, button chase.");

		var globalColors = new[]
		{
			PadColor.Red,
			PadColor.Green,
			PadColor.Blue,
			PadColor.White,
		};

		foreach (var color in globalColors)
		{
			await TrySetAllLedsAsync(color, 127, "self-test-global", cancellationToken).ConfigureAwait(false);
			await Task.Delay(220, cancellationToken).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-reset", cancellationToken).ConfigureAwait(false);

		for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
		{
			await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-pad-reset", cancellationToken).ConfigureAwait(false);
			await TrySetPadColorAsync(pad, GetPadBaseColor(pad)).ConfigureAwait(false);
			_logger.LogInformation("Self-test pad chase: P{Pad,2}", pad);
			await Task.Delay(120, cancellationToken).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-before-buttons", cancellationToken).ConfigureAwait(false);

		for (var button = 0; button < MaschineDeviceConstants.MikroMk3ButtonCount; button++)
		{
			await TrySetButtonLedAsync(button, 127).ConfigureAwait(false);
			_logger.LogInformation("Self-test button chase: B{Button,2}", button);
			await Task.Delay(80, cancellationToken).ConfigureAwait(false);
			await TrySetButtonLedAsync(button, 0).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-complete", cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("LED self-test complete. Interactive mode continues.");
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private static PadColor GetPadBaseColor(int rawPadIndex)
	{
		var mappedIndex = MapPadIndexWithVerticalFlip(rawPadIndex);
		return s_padColors[mappedIndex];
	}

	private static int MapPadIndexWithVerticalFlip(int rawPadIndex)
	{
		var row = rawPadIndex / 4;
		var col = rawPadIndex % 4;
		var flippedRow = 3 - row;
		return (flippedRow * 4) + col;
	}

	private static int ToUserPadNumber(int rawPadIndex)
		=> MapPadIndexWithVerticalFlip(rawPadIndex) + 1;

	private static string FormatColor(PadColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

	private static string FormatButtonState(Maschine.Api.Models.ButtonState state)
	{
		var buttonLabel = state.Button?.GetDisplayNameSingleLine() ?? "Unknown";
		var buttonEnumName = state.Button?.ToString() ?? "Unknown";
		var buttonEnumValue = state.Button?.ToCustomNumber().ToString(CultureInfo.InvariantCulture) ?? "n/a";
		return $"custom={state.Index}, enum={buttonEnumName}({buttonEnumValue}), label={buttonLabel}";
	}

	private static void InvertBitmap(byte[] bitmap)
	{
		for (var i = 0; i < bitmap.Length; i++)
		{
			bitmap[i] = (byte)~bitmap[i];
		}
	}

	private static void UpdateFakeAudioWidgets(DotMatrixDashboard dashboard, int frame)
	{
		var time = frame / 30.0;
		foreach (var widget in dashboard.Widgets)
		{
			switch (widget)
			{
				case VuWidget vu:
				{
					var phase = (Math.Abs(vu.Id.GetHashCode()) % 13) * 0.37;
					var signal = 0.5 + (0.5 * Math.Sin((time * 2.8) + phase));
					var wobble = 0.12 * Math.Sin((time * 11.0) + (phase * 2));
					vu.Advance((float)Math.Clamp(signal + wobble, 0.0, 1.0));
					break;
				}

				case SpectrumWidget spectrum:
				{
					var bandCount = spectrum.BandLevels.Count;
					if (bandCount == 0)
					{
						break;
					}

					var levels = new float[bandCount];
					for (var i = 0; i < bandCount; i++)
					{
						var norm = i / Math.Max(1.0, bandCount - 1.0);
						var sweep = 0.5 + (0.5 * Math.Sin((time * 3.3) + (norm * 8.0)));
						var bass = 0.3 * Math.Sin((time * 1.2) + (norm * 2.0));
						var sparkle = 0.12 * Math.Sin((time * 14.0) + (i * 0.7));
						levels[i] = (float)Math.Clamp((0.1 + (0.8 * sweep) + bass + sparkle), 0.0, 1.0);
					}

					spectrum.Advance(levels);
					break;
				}
			}
		}
	}

	private void PrintMappings()
	{
		_logger.LogInformation("=== Maschine Mikro MK3 Reactive Demo ===");
		_logger.LogInformation("Behavior:");
		_logger.LogInformation("  Button press  -> cycles brightness: off -> mid -> full");
		_logger.LogInformation("  Pad press     -> flash white on press, restore color on release");
		_logger.LogInformation("  Knob          -> selects the active Dashboard by absolute position");
		_logger.LogInformation("  Logo button   -> toggles Dashboard invert mode");
		_logger.LogInformation("  Slider        -> updates strip LEDs and logs position");
		_logger.LogInformation("  Dashboard     -> whole display made of non-overlapping Widgets");
		_logger.LogInformation("  Demo pages     -> {DashboardCount} Dashboards available", _dashboards.Length);
	}
}

