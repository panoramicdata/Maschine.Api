using Maschine.Api;
using Maschine.Api.Interfaces;
using Maschine.Api.Models;

namespace Maschine.Demo;

/// <summary>
/// Cross-mapping demo: every input element is randomly mapped (seed 42) to a different
/// output element on the same device.
///
/// Mappings (all seeded with 42 so they're identical on every run):
///   Button press   → cycle the hue of the mapped pad through a rainbow
///   Pad hit        → pulse the brightness of the mapped button (pressure → brightness)
///   Encoder turn   → rotate the hue of the mapped pad (CW +, CCW -)
/// </summary>
internal sealed class DemoController : IAsyncDisposable
{
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
	}

	// ── Public API ──────────────────────────────────────────────────────────

	internal async Task RunAsync(CancellationToken cancellationToken, bool runLedSelfTest = false)
	{
		PrintMappings();

		await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);

		_buttons = _client.Buttons;
		_pads = _client.Pads;
		_encoders = _client.Encoders;

		_buttons.ButtonChanged += OnButtonChanged;
		_pads.PadChanged += OnPadChanged;
		_encoders.EncoderChanged += OnEncoderChanged;
		_subscribed = true;

		Console.WriteLine("\nDevice connected.  Press Ctrl+C to exit.\n");

		// Blank all LEDs at startup
		await TrySetAllLedsAsync(new PadColor(0, 0, 0), 0, "startup", cancellationToken).ConfigureAwait(false);

		if (runLedSelfTest)
		{
			await RunLedSelfTestAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Normal exit
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

		await Task.CompletedTask.ConfigureAwait(false);
	}

	// ── Event handlers ──────────────────────────────────────────────────────

	private void OnButtonChanged(object? sender, Maschine.Api.Models.ButtonState state)
	{
		if (!state.IsPressed)
		{
			return; // act on press only
		}

		var padIndex = _buttonToPad[state.Index];
		_padHueIndex[padIndex] = (_padHueIndex[padIndex] + 1) % s_rainbow.Length;
		var color = s_rainbow[_padHueIndex[padIndex]];

		Console.WriteLine($"Button {state.Index,2} pressed -> Pad {padIndex,2} color -> {FormatColor(color)}");
		_ = TrySetPadColorAsync(padIndex, color);
	}

	private void OnPadChanged(object? sender, PadState state)
	{
		// Only respond when pressure exceeds a threshold (treat as a "hit")
		if (state.Pressure < 256)
		{
			return;
		}

		var buttonIndex = _padToButton[state.Index];
		// Map pressure (0-4095) to brightness (0-127)
		var brightness = (byte)Math.Clamp(state.Pressure * 127 / 4095, 0, 127);
		_buttonBrightness[buttonIndex] = brightness;

		Console.WriteLine($"Pad {state.Index,2} hit (pressure {state.Pressure,4}) -> Button {buttonIndex,2} brightness -> {brightness,3}");
		_ = TrySetButtonLedAsync(buttonIndex, brightness);
	}

	private void OnEncoderChanged(object? sender, EncoderDelta delta)
	{
		var padIndex = _encoderToPad[delta.Index];
		// Each encoder step advances or retreats the hue by delta steps
		_padHueIndex[padIndex] = ((_padHueIndex[padIndex] + delta.Delta + s_rainbow.Length * 100) % s_rainbow.Length);
		var color = s_rainbow[_padHueIndex[padIndex]];

		var direction = delta.Delta > 0 ? "CW" : "CCW";
		Console.WriteLine($"Encoder {delta.Index} turned {direction} ({delta.Delta:+#;-#;0}) -> Pad {padIndex,2} hue -> {FormatColor(color)}");
		_ = TrySetPadColorAsync(padIndex, color);
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
		Console.WriteLine("=== Maschine Mikro MK3 Cross-Mapping Demo ===");
		Console.WriteLine("(All mappings are fixed for seed 42)");
		Console.WriteLine();

		Console.WriteLine("Buttons → Pads (button press cycles pad hue):");
		for (var b = 0; b < MaschineDeviceConstants.MikroMk3ButtonCount; b++)
		{
			Console.Write($"  B{b,2}→P{_buttonToPad[b]}");
			if ((b + 1) % 9 == 0)
			{
				Console.WriteLine();
			}
		}

		Console.WriteLine();

		Console.WriteLine("Pads → Buttons (pad hit sets button brightness from pressure):");
		for (var p = 0; p < MaschineDeviceConstants.MikroMk3PadCount; p++)
		{
			Console.Write($"  P{p,2}→B{_padToButton[p],2}");
			if ((p + 1) % 8 == 0)
			{
				Console.WriteLine();
			}
		}

		Console.WriteLine();

		Console.WriteLine("Encoders → Pads (encoder turn rotates pad hue):");
		for (var e = 0; e < MaschineDeviceConstants.MikroMk3EncoderCount; e++)
		{
			Console.Write($"  E{e}→P{_encoderToPad[e]}");
		}

		Console.WriteLine("\n");
	}
}

