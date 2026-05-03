using Maschine.Api;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;

namespace Maschine.Demo;

/// <summary>
/// Reactive demo for Maschine Mikro MK3.
///
/// Interactions:
///   Button press   → cycle that button LED through 3 brightness levels
///   Pad hit        → play a random colour/effect on that pad
///   Encoder turn   → logs movement and updates the touch-strip LED meter
/// </summary>
internal sealed class DemoController : IAsyncDisposable
{
	private static readonly int[] s_touchStripLedButtons = [36, 37, 38, 39, 40, 41, 42, 43, 44];
	private static readonly byte[] s_buttonBrightnessCycle = [0, 64, 127, 255];

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

	// ── Zebra speed table: delay in ms for |velocity| = 1..5 ────────────────

	private static readonly int[] s_zebraSpeedMs = [600, 200, 100, 50, 25];

	// ── Random cross-mappings (built once in constructor) ───────────────────

	/// <summary>buttonToPad[b] = the pad index that button b controls.</summary>
	private readonly int[] _buttonToPad;

	/// <summary>padToButton[p] = the button index that pad p controls.</summary>
	private readonly int[] _padToButton;

	/// <summary>encoderToPad[e] = the pad index that encoder e controls.</summary>
	private readonly int[] _encoderToPad;

	// ── Per-element state ───────────────────────────────────────────────────

	private readonly int[] _padCycleState;    // 0=off, 1=white, 2=color; advances on press
	private readonly byte[] _buttonBrightness; // current brightness per button
	private readonly bool[] _padDown;
	private readonly DateTime[] _lastEncoderLogUtc;
	private readonly SemaphoreSlim _touchStripUpdateGate = new(1, 1);
	private readonly object _animationSync = new();
	private int _zebraVelocity = 3;           // -5..+5; sign=direction, |v|=speed (1=slow…5=fast)
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

		_padCycleState = new int[MaschineDeviceConstants.MikroMk3PadCount];
		_buttonBrightness = new byte[MaschineDeviceConstants.MikroMk3ButtonCount];
		_padDown = new bool[MaschineDeviceConstants.MikroMk3PadCount];
		_lastEncoderLogUtc = new DateTime[MaschineDeviceConstants.MikroMk3EncoderCount];
	}

	// ── Public API ──────────────────────────────────────────────────────────

	internal async Task RunAsync(
		CancellationToken cancellationToken,
		bool runLedSelfTest = false,
		bool runFullBrightness = false,
		bool runPadColorSpace = false,
		bool runDisplayTest = false,
		bool runDisplayZebra = false,
		bool runDisplayZebraAnimate = false)
	{
		PrintMappings();

		await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);

		_buttons = _client.Buttons;
		_pads = _client.Pads;
		_encoders = _client.Encoders;

		if (!runFullBrightness && !runPadColorSpace)
		{
			_buttons.ButtonChanged += OnButtonChanged;
			_buttons.EncoderTouchChanged += OnEncoderTouchChanged;
			_pads.PadChanged += OnPadChanged;
			_encoders.EncoderChanged += OnEncoderChanged;
			_subscribed = true;
		}

		Console.WriteLine("\nDevice connected.  Press Ctrl+C to exit.\n");

		// Blank all LEDs at startup
		await TrySetAllLedsAsync(new PadColor(0, 0, 0), 0, "startup", cancellationToken).ConfigureAwait(false);
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
			Console.WriteLine("All pads/buttons set to full brightness (interactive mappings disabled).");
		}

		if (runPadColorSpace)
		{
			Console.WriteLine("Pad color-space mode enabled (interactive mappings disabled).");
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

		await _client.DisconnectAsync().ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		if (_subscribed && _buttons is not null && _pads is not null && _encoders is not null)
		{
			_buttons.ButtonChanged -= OnButtonChanged;
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
		var touchStr = state.IsTouched ? "touched" : "released";
		Console.WriteLine($"Volume knob {touchStr}, position={state.KnobValue}");
	}

	private void OnButtonChanged(object? sender, Maschine.Api.Models.ButtonState state)
	{
		if (!state.IsPressed)
		{
			return; // act on press only
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

		Console.WriteLine($"Button {state.Index,2} pressed -> brightness {nextBrightness}");
		_ = TrySetButtonLedAsync(state.Index, nextBrightness);
	}

	private void OnPadChanged(object? sender, PadState state)
	{
		const int PressThreshold = 450;
		const int ReleaseThreshold = 120;

		if (state.Pressure <= ReleaseThreshold)
		{
			lock (_animationSync)
			{
				_padDown[state.Index] = false;
			}

			return;
		}

		bool shouldTrigger;
		lock (_animationSync)
		{
			shouldTrigger = !_padDown[state.Index] && state.Pressure >= PressThreshold;
			if (shouldTrigger)
			{
				_padDown[state.Index] = true;
			}
		}

		if (!shouldTrigger)
		{
			return;
		}

		int cycleState;
		lock (_animationSync)
		{
			_padCycleState[state.Index] = (_padCycleState[state.Index] + 1) % 3;
			cycleState = _padCycleState[state.Index];
		}

		// 0=off, 1=white, 2=color  (cycle: off → white → color → off)
		var color = cycleState switch
		{
			1 => PadColor.White,
			2 => s_padColors[state.Index],
			_ => PadColor.Off,
		};

		var label = cycleState switch { 1 => "white", 2 => "color", _ => "off" };
		Console.WriteLine($"Pad {state.Index,2} -> {label}");
		_ = TrySetPadColorAsync(state.Index, color);
	}

	private void OnEncoderChanged(object? sender, EncoderDelta delta)
	{
		const int NoiseFloor = 8;
		const int LogThrottleMs = 60;

		if (Math.Abs(delta.Delta) < NoiseFloor)
		{
			return;
		}

		var step = Math.Sign(delta.Delta);
		if (step == 0)
		{
			return;
		}

		if (delta.Index == 8)
		{
			// Touch fader → controls touch-strip LEDs
			var nowUtc = DateTime.UtcNow;
			bool shouldLog;
			lock (_animationSync)
			{
				shouldLog = (nowUtc - _lastEncoderLogUtc[delta.Index]).TotalMilliseconds >= LogThrottleMs;
				if (shouldLog)
				{
					_lastEncoderLogUtc[delta.Index] = nowUtc;
				}

				_touchStripLevel = Math.Clamp(_touchStripLevel + step, 0, s_touchStripLedButtons.Length);
			}

			if (shouldLog)
			{
				Console.WriteLine($"Touch fader moved ({delta.Delta:+#;-#;0})");
			}

			_ = UpdateTouchStripLedsCoalescedAsync();
		}
		else
		{
			// Main knob → controls zebra animation speed/direction (-5..+5)
			int newVelocity;
			lock (_animationSync)
			{
				_zebraVelocity = Math.Clamp(_zebraVelocity + step, -5, 5);
				newVelocity = _zebraVelocity;
			}

			var dirStr = newVelocity == 0
				? "stopped"
				: newVelocity > 0 ? $"fwd ×{newVelocity}" : $"rev ×{-newVelocity}";
			Console.WriteLine($"Zebra: {dirStr}");
		}
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



	private async Task TrySetPadColorSpaceAsync(CancellationToken cancellationToken)
	{
		if (_pads is null)
		{
			return;
		}

		var palette = s_padColors;

		try
		{
			for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
			{
				var color = palette[pad];
				await _pads.SetColorAsync(pad, color, cancellationToken).ConfigureAwait(false);
			}

			Console.WriteLine("Pad color-space written across all 16 pads:");
			for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
			{
				var color = palette[pad];
				Console.WriteLine($"  P{pad,2} -> {FormatColor(color)}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[warn] Pad color-space write failed: {ex.Message}");
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
		Console.WriteLine("Dot-matrix zebra animation started. Turn the main knob to change speed/direction.");
		var phase = 0;
		while (!cancellationToken.IsCancellationRequested)
		{
			int v;
			lock (_animationSync)
			{
				v = _zebraVelocity;
			}

			if (v != 0)
			{
				phase = (phase + Math.Sign(v) + 8) & 7;
				await _client.SetDotMatrixZebraLinesAsync(phase, cancellationToken).ConfigureAwait(false);
			}

			var delayMs = v == 0 ? 100 : s_zebraSpeedMs[Math.Abs(v) - 1];
			await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
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
			await TrySetPadColorAsync(pad, s_padColors[pad % s_padColors.Length]).ConfigureAwait(false);
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
		Console.WriteLine("  Button press  -> cycles brightness: off -> mid -> full");
		Console.WriteLine("  Pad press     -> cycles: white -> color -> off");
		Console.WriteLine("  Main knob     -> changes zebra speed/direction (incl. reverse)");
		Console.WriteLine("  Touch fader   -> animates touch-strip LEDs");
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

