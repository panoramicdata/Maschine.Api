using Maschine.Api;
using Maschine.Api.Models;
using Maschine.Demo;
using Maschine.Api.Exceptions;
using Microsoft.Extensions.Logging;
using System.Threading;

using var cts = new CancellationTokenSource();
var cancelPressCount = 0;

// Default to unified light output so the demo works out-of-the-box on Mikro MK3
// without requiring --force-unified.
var options = new MaschineClientOptions
{
	ForceUnifiedLightOutput = true,
	KeyModes = KeyModeDefaults.Create(),
	KeyRadioButtonGroups =
	[
		new RadioButtonGroupOptions
		{
			Mode = RadioButtonGroupMode.AlwaysOneOn,
			Keys = [MikroMk3Button.PadMode, MikroMk3Button.Keyboard, MikroMk3Button.Chords],
		},
	],
};

options.KeyModes[MikroMk3Button.MachineLogo] = KeyMode.FireEarly;
options.KeyModes[MikroMk3Button.PadMode] = KeyMode.FireEarly;
options.KeyModes[MikroMk3Button.Keyboard] = KeyMode.FireEarly;
options.KeyModes[MikroMk3Button.Chords] = KeyMode.FireEarly;

static int? ParseIntOption(string[] sourceArgs, string optionName)
{
	var withEquals = sourceArgs.FirstOrDefault(a =>
		a.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase));
	if (withEquals is not null)
	{
		var valueText = withEquals[(optionName.Length + 1)..];
		if (int.TryParse(valueText, out var parsed))
		{
			return parsed;
		}

		throw new ArgumentException($"Invalid value for {optionName}: '{valueText}'. Expected integer.");
	}

	for (var i = 0; i < sourceArgs.Length - 1; i++)
	{
		if (!sourceArgs[i].Equals(optionName, StringComparison.OrdinalIgnoreCase))
		{
			continue;
		}

		var valueText = sourceArgs[i + 1];
		if (int.TryParse(valueText, out var parsed))
		{
			return parsed;
		}

		throw new ArgumentException($"Invalid value for {optionName}: '{valueText}'. Expected integer.");
	}

	return null;
}

static string? ParseStringOption(string[] sourceArgs, string optionName)
{
	var withEquals = sourceArgs.FirstOrDefault(a =>
		a.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase));
	if (withEquals is not null)
	{
		return withEquals[(optionName.Length + 1)..];
	}

	for (var i = 0; i < sourceArgs.Length - 1; i++)
	{
		if (sourceArgs[i].Equals(optionName, StringComparison.OrdinalIgnoreCase))
		{
			return sourceArgs[i + 1];
		}
	}

	return null;
}

var runLedSelfTest = args.Any(a =>
	a.Equals("--led-test", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--self-test", StringComparison.OrdinalIgnoreCase));

var runFullBrightness = args.Any(a =>
	a.Equals("--full-brightness", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--all-bright", StringComparison.OrdinalIgnoreCase));

var runPadColorSpace = args.Any(a =>
	a.Equals("--pad-color-space", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--pad-colorspace", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--pad-gamut", StringComparison.OrdinalIgnoreCase));

var traceInput = args.Any(a =>
	a.Equals("--trace-input", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--trace-reports", StringComparison.OrdinalIgnoreCase));

var traceOnly = args.Any(a =>
	a.Equals("--trace-only", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--raw-only", StringComparison.OrdinalIgnoreCase));

var listDevices = args.Any(a =>
	a.Equals("--list-devices", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--enumerate", StringComparison.OrdinalIgnoreCase));

var deviceIndexOverride = ParseIntOption(args, "--device-index");

var logLevelText = ParseStringOption(args, "--logLevel")
	?? ParseStringOption(args, "--log-level")
	?? "Info";

if (!Enum.TryParse<LogLevel>(logLevelText, ignoreCase: true, out var configuredLogLevel))
{
	configuredLogLevel = LogLevel.Information;
}

using var loggerFactory = LoggerFactory.Create(builder =>
{
	builder
		.SetMinimumLevel(configuredLogLevel)
		.AddSimpleConsole(options =>
		{
			options.TimestampFormat = "HH:mm:ss.fff ";
			options.UseUtcTimestamp = true;
			options.SingleLine = true;
		});
});

var logger = loggerFactory.CreateLogger("Maschine.Demo");

var forceUnified = args.Any(a => a.Equals("--force-unified", StringComparison.OrdinalIgnoreCase));
const bool runDisplayTest = false;
const bool runDisplayZebra = false;
const bool runDisplayShowcase = true;

if (runLedSelfTest || runFullBrightness)
{
	// Known-good path for this hardware family when legacy split writes do not visibly update LEDs.
	options.ForceUnifiedLightOutput = true;
}

if (forceUnified)
{
	options.ForceUnifiedLightOutput = true;
}

if (deviceIndexOverride.HasValue)
{
	if (deviceIndexOverride.Value < 0)
	{
		logger.LogError("--device-index must be >= 0.");
		return 1;
	}

	options.DeviceIndex = deviceIndexOverride.Value;
}

options.TraceInputReports = traceInput || traceOnly || configuredLogLevel <= LogLevel.Debug;

if (listDevices)
{
	var enumerator = new HidDeviceEnumerator();
	var devices = enumerator.Enumerate(options.VendorId, options.ProductId);
	if (devices.Count == 0)
	{
		logger.LogInformation("No matching Maschine HID devices were found.");
		return 0;
	}

	logger.LogInformation("Found {Count} matching HID device(s):", devices.Count);
	for (var i = 0; i < devices.Count; i++)
	{
		var descriptor = devices[i].SerialNumber
			?? $"VID=0x{devices[i].VendorId:X4} PID=0x{devices[i].ProductId:X4}";
		logger.LogInformation("  [{Index}] {Descriptor}", i, descriptor);
	}

	logger.LogInformation("Use --device-index <n> to select a specific entry.");
	return 0;
}

using var client = new MaschineClient(options, loggerFactory.CreateLogger<MaschineClient>());
await using var demo = new DemoController(client, loggerFactory.CreateLogger<DemoController>());

void TryBlankSurface()
{
	try
	{
		client.ClearDotMatrixAsync(CancellationToken.None).GetAwaiter().GetResult();
		Thread.Sleep(20);
		client.ClearDotMatrixAsync(CancellationToken.None).GetAwaiter().GetResult();
		client.Pads.SetAllColorsAsync(PadColor.Off, CancellationToken.None).GetAwaiter().GetResult();
		client.Buttons.SetAllLedsAsync(0, CancellationToken.None).GetAwaiter().GetResult();
	}
	catch
	{
		// Device may already be disconnected or not yet connected.
	}
}

AppDomain.CurrentDomain.ProcessExit += (_, _) => TryBlankSurface();

Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	if (Interlocked.Increment(ref cancelPressCount) == 1)
	{
		logger.LogInformation("Ctrl+C received, shutting down...");
		cts.Cancel();
		return;
	}

	logger.LogWarning("Force exit requested.");
	TryBlankSurface();
	Thread.Sleep(40);
	TryBlankSurface();
	Environment.Exit(130);
};

try
{
	if (runLedSelfTest)
	{
		logger.LogInformation("LED self-test mode enabled.");
	}

	if (runFullBrightness)
	{
		logger.LogInformation("Full-brightness mode enabled.");
	}

	if (runPadColorSpace)
	{
		logger.LogInformation("Pad color-space mode enabled.");
	}

	if (options.ForceUnifiedLightOutput)
	{
		logger.LogInformation("Unified light output forced.");
	}

	if (deviceIndexOverride.HasValue)
	{
		logger.LogInformation("Using HID device index {DeviceIndex}.", options.DeviceIndex);
	}

	if (traceInput)
	{
		logger.LogInformation("Raw input report tracing enabled.");
	}

	if (traceOnly)
	{
		logger.LogInformation("Trace-only mode enabled (demo logic disabled).");
	}

	logger.LogInformation("Configured log level: {LogLevel}", configuredLogLevel);

	logger.LogInformation("Dot-matrix display showcase enabled (default).");

	if (traceOnly)
	{
		await client.ConnectAsync(cts.Token);
		logger.LogInformation("Trace-only capture active. Interact with controls and press Ctrl+C to stop.");
		try
		{
			await Task.Delay(Timeout.Infinite, cts.Token);
		}
		finally
		{
			await client.DisconnectAsync();
		}
	}
	else
	{
		await demo.RunAsync(cts.Token, runLedSelfTest, runFullBrightness, runPadColorSpace, runDisplayTest, runDisplayZebra, runDisplayShowcase);
	}
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
	// Normal Ctrl+C shutdown.
	return 0;
}
catch (MaschineDeviceNotFoundException)
{
	logger.LogError("No Maschine Mikro MK3 device found. Please connect the device and try again.");
	return 1;
}
catch (Exception ex)
{
	logger.LogError(ex, "Unexpected error.");
	return 1;
}
finally
{
	TryBlankSurface();
	Thread.Sleep(40);
	TryBlankSurface();
}

return 0;
