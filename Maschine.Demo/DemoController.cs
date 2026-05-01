using Maschine.Api;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;

namespace Maschine.Demo;

/// <summary>
/// Reactive demo for Maschine Mikro MK3.
///
/// Interactions:
///   Button press   → toggle that button LED on/off with a short fade
///   Pad hit        → play a random colour/effect on that pad
///   Encoder turn   → logs movement and updates the touch-strip LED meter
/// </summary>
internal sealed class DemoController : IAsyncDisposable
{
	private static readonly int[] s_touchStripLedButtons = [36, 37, 38, 39, 40, 41, 42, 43, 44];

	// ── Hue palette (12 evenly-spaced rainbow colours) ─────────────────────

	private static readonly PadColor[] s_rainbow =
	[
		new(255,   0,   0),   // 0  red
		new(255, 128,   0),   // 1  orange
		new(255, 255,   0),   // 2  yellow
		new(128, 255,   0),   // 3  lime
		new(  0, 255,   0),   // 4  green
		new(  0, 255, 128),   // 5  spring
		new(  0, 255, 255),   // 6  cyan
		new(  0, 128, 255),   // 7  azure
		new(  0,   0, 255),   // 8  blue
		new(128,   0, 255),   // 9  violet
		new(255,   0, 255),   // 10 magenta
		new(255,   0, 128),   // 11 rose
	];

	// ── Random cross-mappings (built once in constructor) ───────────────────

	/// <summary>buttonToPad[b] = the pad index that button b controls.</summary>
	private readonly int[] _buttonToPad;

	/// <summary>padToButton[p] = the button index that pad p controls.</summary>
	private readonly int[] _padToButton;

	/// <summary>encoderToPad[e] = the pad index that encoder e controls.</summary>
	private readonly int[] _encoderToPad;

	// ── Per-element state ───────────────────────────────────────────────────

	private readonly int[] _padHueIndex;      // current hue index (0-11) per pad
	private readonly byte[] _buttonBrightness; // current brightness per button
	private readonly bool[] _buttonOn;
	private readonly bool[] _padDown;
	private readonly DateTime[] _padLastTriggerUtc;
	private readonly int[] _buttonAnimationGeneration;
	private readonly int[] _padAnimationGeneration;
	private readonly DateTime[] _lastEncoderLogUtc;
	private readonly SemaphoreSlim _touchStripUpdateGate = new(1, 1);
	private readonly object _animationSync = new();
	private readonly Random _random = new();
	private int _touchStripLevel;
	private int _touchStripRenderedLevel = -1;

	private readonly IMaschineClient _client;
	private IButtons? _buttons;
	private IPads? _pads;
	private IEncoders? _encoders;
	private bool _subscribed;

	// ── Construction ────────────────────────────────────────────────────────

	internal DemoController(IMaschineClient client)
	{
		_client = client;

		var rng = new Random(42);

		_buttonToPad = BuildMapping(rng, MaschineDeviceConstants.MikroMk3ButtonCount, MaschineDeviceConstants.MikroMk3PadCount);
		_padToButton = BuildMapping(rng, MaschineDeviceConstants.MikroMk3PadCount, MaschineDeviceConstants.MikroMk3ButtonCount);
		_encoderToPad = BuildMapping(rng, MaschineDeviceConstants.MikroMk3EncoderCount, MaschineDeviceConstants.MikroMk3PadCount);

		_padHueIndex = new int[MaschineDeviceConstants.MikroMk3PadCount];
		_buttonBrightness = new byte[MaschineDeviceConstants.MikroMk3ButtonCount];
		_buttonOn = new bool[MaschineDeviceConstants.MikroMk3ButtonCount];
		_padDown = new bool[MaschineDeviceConstants.MikroMk3PadCount];
		_padLastTriggerUtc = new DateTime[MaschineDeviceConstants.MikroMk3PadCount];
		_buttonAnimationGeneration = new int[MaschineDeviceConstants.MikroMk3ButtonCount];
		_padAnimationGeneration = new int[MaschineDeviceConstants.MikroMk3PadCount];
		_lastEncoderLogUtc = new DateTime[MaschineDeviceConstants.MikroMk3EncoderCount];
	}

	// ── Public API ──────────────────────────────────────────────────────────

	internal async Task RunAsync(
		CancellationToken cancellationToken,
		bool runLedSelfTest = false,
		bool runFullBrightness = false,
		bool runDisplayTest = false,
		bool runDisplayZebra = false,
		bool runDisplayZebraAnimate = false)
	{
		PrintMappings();

		await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);

		_buttons = _client.Buttons;
		_pads = _client.Pads;
		_encoders = _client.Encoders;

		if (!runFullBrightness)
		{
			_buttons.ButtonChanged += OnButtonChanged;
			_pads.PadChanged += OnPadChanged;
			_encoders.EncoderChanged += OnEncoderChanged;
			_subscribed = true;
		}

		Console.WriteLine("\nDevice connected.  Press Ctrl+C to exit.\n");

		// Blank all LEDs at startup
		await TrySetAllLedsAsync(new PadColor(0, 0, 0), 0, "startup", cancellationToken).ConfigureAwait(false);
		await TrySetRandomPadColorsAsync(cancellationToken).ConfigureAwait(false);

		if (runLedSelfTest)
		{
			await RunLedSelfTestAsync(cancellationToken).ConfigureAwait(false);
		}

		if (runFullBrightness)
		{
			await TrySetAllLedsAsync(PadColor.White, 127, "full-brightness", cancellationToken)
				.ConfigureAwait(false);
			Console.WriteLine("All pads/buttons set to full brightness (interactive mappings disabled).");
		}

		if (runDisplayTest)
		{
			await TrySetDotMatrixTestPatternAsync(cancellationToken).ConfigureAwait(false);
		}

		if (runDisplayZebra)
		{
			await TrySetDotMatrixZebraAsync(cancellationToken).ConfigureAwait(false);
		}

		Task? zebraAnimationTask = null;
		if (runDisplayZebraAnimate)
		{
			zebraAnimationTask = RunDotMatrixZebraAnimationAsync(cancellationToken);
		}

		try
		{
			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Normal exit
		}

		if (zebraAnimationTask is not null)
		{
			try
			{
				await zebraAnimationTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Normal exit
			}
		}

		if (_subscribed && _buttons is not null && _pads is not null && _encoders is not null)
		{
			_buttons.ButtonChanged -= OnButtonChanged;
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

		await _client.DisconnectAsync().ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		if (_subscribed && _buttons is not null && _pads is not null && _encoders is not null)
		{
			_buttons.ButtonChanged -= OnButtonChanged;
			_pads.PadChanged -= OnPadChanged;
			_encoders.EncoderChanged -= OnEncoderChanged;
			_subscribed = false;
		}

		_touchStripUpdateGate.Dispose();

		await Task.CompletedTask.ConfigureAwait(false);
	}

	// ── Event handlers ──────────────────────────────────────────────────────

	private void OnButtonChanged(object? sender, Maschine.Api.Models.ButtonState state)
	{
		if (!state.IsPressed)
		{
			return; // act on press only
		}

		int generation;
		bool targetOn;
		lock (_animationSync)
		{
			targetOn = !_buttonOn[state.Index];
			_buttonOn[state.Index] = targetOn;
			generation = ++_buttonAnimationGeneration[state.Index];
		}

		Console.WriteLine($"Button {state.Index,2} pressed -> {(targetOn ? "ON" : "OFF")} (fade)");
		_ = AnimateButtonToggleAsync(state.Index, targetOn, generation);
	}

	private void OnPadChanged(object? sender, PadState state)
	{
		const int PressThreshold = 450;
		const int ReleaseThreshold = 120;
		const int DebounceMs = 80;

		if (state.Pressure <= ReleaseThreshold)
		{
			lock (_animationSync)
			{
				_padDown[state.Index] = false;
			}

			return;
		}

		var nowUtc = DateTime.UtcNow;
		bool shouldTrigger;
		int generation;
		lock (_animationSync)
		{
			var sinceLastTrigger = nowUtc - _padLastTriggerUtc[state.Index];
			shouldTrigger = !_padDown[state.Index]
				&& state.Pressure >= PressThreshold
				&& sinceLastTrigger.TotalMilliseconds >= DebounceMs;
			if (shouldTrigger)
			{
				_padDown[state.Index] = true;
				_padLastTriggerUtc[state.Index] = nowUtc;
				generation = ++_padAnimationGeneration[state.Index];
			}
			else
			{
				generation = _padAnimationGeneration[state.Index];
			}
		}

		if (!shouldTrigger)
		{
			return;
		}

		Console.WriteLine($"Pad {state.Index,2} pressed -> random pad effect");
		_ = PrepareAndAnimatePadPressAsync(state.Index, generation);
	}

	private async Task PrepareAndAnimatePadPressAsync(int padIndex, int generation)
	{
		await CancelOtherPadAnimationsAsync(padIndex).ConfigureAwait(false);
		await AnimatePadPressAsync(padIndex, generation).ConfigureAwait(false);
	}

	private async Task AnimateButtonToggleAsync(int buttonIndex, bool targetOn, int generation)
	{
		byte start;
		lock (_animationSync)
		{
			start = _buttonBrightness[buttonIndex];
		}

		var target = targetOn ? 127 : 0;
		const int steps = 24;

		for (var i = 1; i <= steps; i++)
		{
			lock (_animationSync)
			{
				if (_buttonAnimationGeneration[buttonIndex] != generation)
				{
					return;
				}
			}

			var value = (byte)(start + ((target - start) * i / steps));
			await TrySetButtonLedAsync(buttonIndex, value).ConfigureAwait(false);

			lock (_animationSync)
			{
				_buttonBrightness[buttonIndex] = value;
			}

			await Task.Delay(12).ConfigureAwait(false);
		}
	}

	private void OnEncoderChanged(object? sender, EncoderDelta delta)
	{
		const int NoiseFloor = 8;
		const int LogThrottleMs = 60;

		if (Math.Abs(delta.Delta) < NoiseFloor)
		{
			return;
		}

		var nowUtc = DateTime.UtcNow;
		bool shouldLog;
		lock (_animationSync)
		{
			shouldLog = (nowUtc - _lastEncoderLogUtc[delta.Index]).TotalMilliseconds >= LogThrottleMs;
			if (shouldLog)
			{
				_lastEncoderLogUtc[delta.Index] = nowUtc;
			}
		}

		if (shouldLog)
		{
			if (delta.Index == 8)
			{
				Console.WriteLine($"Touch fader moved ({delta.Delta:+#;-#;0})");
			}
			else
			{
				var direction = delta.Delta > 0 ? "CW" : "CCW";
				Console.WriteLine($"Encoder {delta.Index} turned {direction} ({delta.Delta:+#;-#;0})");
			}
		}

		var step = Math.Sign(delta.Delta);
		if (step == 0)
		{
			return;
		}

		lock (_animationSync)
		{
			_touchStripLevel = Math.Clamp(_touchStripLevel + step, 0, s_touchStripLedButtons.Length);
		}

		_ = UpdateTouchStripLedsCoalescedAsync();
	}

	private async Task UpdateTouchStripLedsCoalescedAsync()
	{
		if (_buttons is null)
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
					if (_touchStripRenderedLevel < 0)
					{
						for (var i = 0; i < s_touchStripLedButtons.Length; i++)
						{
							var brightness = (byte)(i < level ? 127 : 0);
							await TrySetButtonLedAsync(s_touchStripLedButtons[i], brightness).ConfigureAwait(false);
						}
					}
					else if (level > _touchStripRenderedLevel)
					{
						for (var i = _touchStripRenderedLevel; i < level; i++)
						{
							await TrySetButtonLedAsync(s_touchStripLedButtons[i], 127).ConfigureAwait(false);
						}
					}
					else
					{
						for (var i = level; i < _touchStripRenderedLevel; i++)
						{
							await TrySetButtonLedAsync(s_touchStripLedButtons[i], 0).ConfigureAwait(false);
						}
					}

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

	private async Task AnimatePadPressAsync(int padIndex, int generation)
	{
		int effectIndex;
		PadColor finalColor;
		lock (_animationSync)
		{
			effectIndex = _random.Next(4);
			finalColor = s_rainbow[_random.Next(s_rainbow.Length)];
		}

		switch (effectIndex)
		{
			case 0:
				await PlayPadStrobeAsync(padIndex, generation, finalColor).ConfigureAwait(false);
				break;
			case 1:
				await PlayPadPulseAsync(padIndex, generation, finalColor).ConfigureAwait(false);
				break;
			case 2:
				await PlayPadRainbowSpinAsync(padIndex, generation).ConfigureAwait(false);
				break;
			default:
				await TrySetPadColorIfCurrentAsync(padIndex, generation, finalColor).ConfigureAwait(false);
				break;
		}
	}

	private async Task PlayPadStrobeAsync(int padIndex, int generation, PadColor finalColor)
	{
		for (var i = 0; i < 3; i++)
		{
			await TrySetPadColorIfCurrentAsync(padIndex, generation, PadColor.White).ConfigureAwait(false);
			await Task.Delay(40).ConfigureAwait(false);
			await TrySetPadColorIfCurrentAsync(padIndex, generation, PadColor.Off).ConfigureAwait(false);
			await Task.Delay(30).ConfigureAwait(false);
		}

		await TrySetPadColorIfCurrentAsync(padIndex, generation, finalColor).ConfigureAwait(false);
	}

	private async Task PlayPadPulseAsync(int padIndex, int generation, PadColor finalColor)
	{
		await TrySetPadColorIfCurrentAsync(padIndex, generation, finalColor).ConfigureAwait(false);
		await Task.Delay(80).ConfigureAwait(false);
		await TrySetPadColorIfCurrentAsync(padIndex, generation, PadColor.Off).ConfigureAwait(false);
		await Task.Delay(60).ConfigureAwait(false);
		await TrySetPadColorIfCurrentAsync(padIndex, generation, finalColor).ConfigureAwait(false);
	}

	private async Task PlayPadRainbowSpinAsync(int padIndex, int generation)
	{
		for (var i = 0; i < 5; i++)
		{
			var color = s_rainbow[(padIndex + i) % s_rainbow.Length];
			await TrySetPadColorIfCurrentAsync(padIndex, generation, color).ConfigureAwait(false);
			await Task.Delay(45).ConfigureAwait(false);
		}
	}

	private async Task TrySetPadColorIfCurrentAsync(int padIndex, int generation, PadColor color)
	{
		lock (_animationSync)
		{
			if (_padAnimationGeneration[padIndex] != generation)
			{
				return;
			}
		}

		await TrySetPadColorAsync(padIndex, color).ConfigureAwait(false);
	}

	private async Task CancelOtherPadAnimationsAsync(int activePadIndex)
	{
		if (_pads is null)
		{
			return;
		}

		for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
		{
			if (pad == activePadIndex)
			{
				continue;
			}

			lock (_animationSync)
			{
				_padAnimationGeneration[pad]++;
			}

			await TrySetPadColorAsync(pad, PadColor.Off).ConfigureAwait(false);
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
			await _pads.SetAllColorsAsync(padColor, cancellationToken).ConfigureAwait(false);
			await _buttons.SetAllLedsAsync(buttonBrightness, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[warn] LED write failed during {phase}: {ex.Message}");
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
			await _pads.SetColorAsync(padIndex, color).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[warn] Pad write failed for P{padIndex}: {ex.Message}");
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
			Console.WriteLine($"[warn] Button write failed for B{buttonIndex}: {ex.Message}");
		}
	}

	private async Task TrySetRandomPadColorsAsync(CancellationToken cancellationToken)
	{
		if (_pads is null)
		{
			return;
		}

		try
		{
			for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
			{
				var color = new PadColor(
					(byte)_random.Next(32, 256),
					(byte)_random.Next(32, 256),
					(byte)_random.Next(32, 256));

				await _pads.SetColorAsync(pad, color, cancellationToken).ConfigureAwait(false);
			}

			Console.WriteLine("Startup: all pads set to random colors.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[warn] Random startup pad colors failed: {ex.Message}");
		}
	}

	private async Task TrySetDotMatrixTestPatternAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.SetDotMatrixTestPatternAsync(cancellationToken).ConfigureAwait(false);
			Console.WriteLine("Dot-matrix test pattern written.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[warn] Dot-matrix write failed: {ex.Message}");
		}
	}

	private async Task TrySetDotMatrixZebraAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.SetDotMatrixZebraLinesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			Console.WriteLine("Dot-matrix zebra pattern written.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[warn] Dot-matrix zebra write failed: {ex.Message}");
		}
	}

	private async Task RunDotMatrixZebraAnimationAsync(CancellationToken cancellationToken)
	{
		Console.WriteLine("Dot-matrix zebra animation started.");
		var phase = 0;
		while (!cancellationToken.IsCancellationRequested)
		{
			await _client.SetDotMatrixZebraLinesAsync(phase, cancellationToken).ConfigureAwait(false);
			phase = (phase + 1) & 7;
			await Task.Delay(80, cancellationToken).ConfigureAwait(false);
		}
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
		Console.WriteLine("Running LED self-test: global colors, pad chase, button chase.");

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
			await TrySetPadColorAsync(pad, s_rainbow[pad % s_rainbow.Length]).ConfigureAwait(false);
			Console.WriteLine($"Self-test pad chase: P{pad,2}");
			await Task.Delay(120, cancellationToken).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-before-buttons", cancellationToken).ConfigureAwait(false);

		for (var button = 0; button < MaschineDeviceConstants.MikroMk3ButtonCount; button++)
		{
			await TrySetButtonLedAsync(button, 127).ConfigureAwait(false);
			Console.WriteLine($"Self-test button chase: B{button,2}");
			await Task.Delay(80, cancellationToken).ConfigureAwait(false);
			await TrySetButtonLedAsync(button, 0).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-complete", cancellationToken).ConfigureAwait(false);
		Console.WriteLine("LED self-test complete. Interactive mode continues.");
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Builds a surjective mapping from <paramref name="sourceCount"/> indices to
	/// <paramref name="targetCount"/> indices, randomised with the supplied RNG.
	/// </summary>
	private static int[] BuildMapping(Random rng, int sourceCount, int targetCount)
	{
		var map = new int[sourceCount];
		for (var i = 0; i < sourceCount; i++)
		{
			map[i] = rng.Next(targetCount);
		}

		return map;
	}

	private static string FormatColor(PadColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

	private void PrintMappings()
	{
		Console.WriteLine("=== Maschine Mikro MK3 Reactive Demo ===");
		Console.WriteLine("(All mappings are fixed for seed 42)");
		Console.WriteLine();
		Console.WriteLine("Behavior:");
		Console.WriteLine("  Button press -> toggles that same button LED with fade");
		Console.WriteLine("  Pad press -> random effect on that same pad");
		Console.WriteLine("  Encoder and touch fader -> log movement + animate touch-strip LEDs");
		Console.WriteLine();

		Console.WriteLine("Buttons → Pads (reserved random map for future effects):");
		for (var b = 0; b < MaschineDeviceConstants.MikroMk3ButtonCount; b++)
		{
			Console.Write($"  B{b,2}→P{_buttonToPad[b]}");
			if ((b + 1) % 9 == 0)
			{
				Console.WriteLine();
			}
		}

		Console.WriteLine();

		Console.WriteLine("Pads → Buttons (reserved random map for future effects):");
		for (var p = 0; p < MaschineDeviceConstants.MikroMk3PadCount; p++)
		{
			Console.Write($"  P{p,2}→B{_padToButton[p],2}");
			if ((p + 1) % 8 == 0)
			{
				Console.WriteLine();
			}
		}

		Console.WriteLine();

		Console.WriteLine("Encoders → Pads (reserved random map for future effects):");
		for (var e = 0; e < MaschineDeviceConstants.MikroMk3EncoderCount; e++)
		{
			Console.Write($"  E{e}→P{_encoderToPad[e]}");
		}

		Console.WriteLine("\n");
	}
}

