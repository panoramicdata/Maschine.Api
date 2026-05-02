using Maschine.Api;
using Maschine.Api.Models;
using Maschine.Demo;
using Maschine.Api.Exceptions;
using System.Threading;

using var cts = new CancellationTokenSource();
var cancelPressCount = 0;
var shutdownBlankInvoked = 0;

var options = new MaschineClientOptions();
using var client = new MaschineClient(options);
await using var demo = new DemoController(client);

void TryBlankSurface()
{
	if (Interlocked.Exchange(ref shutdownBlankInvoked, 1) != 0)
	{
		return;
	}

	try
	{
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
		Console.WriteLine("Ctrl+C received, shutting down...");
		cts.Cancel();
		Thread.Sleep(180);
		TryBlankSurface();

		try
		{
			client.DisconnectAsync().GetAwaiter().GetResult();
		}
		catch
		{
			// Continue to process exit.
		}

		Environment.Exit(0);

		return;
	}

	Console.WriteLine("Force exit requested.");
	Environment.Exit(130);
};

var runLedSelfTest = args.Any(a =>
	a.Equals("--led-test", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--self-test", StringComparison.OrdinalIgnoreCase));

var runFullBrightness = args.Any(a =>
	a.Equals("--full-brightness", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--all-bright", StringComparison.OrdinalIgnoreCase));

var forceUnified = args.Any(a => a.Equals("--force-unified", StringComparison.OrdinalIgnoreCase));
const bool runDisplayTest = false;
const bool runDisplayZebra = false;
const bool runDisplayZebraAnimate = true;

if (runLedSelfTest || runFullBrightness)
{
	// Known-good path for this hardware family when legacy split writes do not visibly update LEDs.
	options.ForceUnifiedLightOutput = true;
}

if (forceUnified)
{
	options.ForceUnifiedLightOutput = true;
}

try
{
	if (runLedSelfTest)
	{
		Console.WriteLine("LED self-test mode enabled.");
	}

	if (runFullBrightness)
	{
		Console.WriteLine("Full-brightness mode enabled.");
	}

	if (options.ForceUnifiedLightOutput)
	{
		Console.WriteLine("Unified light output forced.");
	}

	Console.WriteLine("Dot-matrix zebra animation enabled (default).");

	await demo.RunAsync(cts.Token, runLedSelfTest, runFullBrightness, runDisplayTest, runDisplayZebra, runDisplayZebraAnimate);
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
	// Normal Ctrl+C shutdown.
	return 0;
}
catch (MaschineDeviceNotFoundException)
{
	Console.Error.WriteLine("No Maschine Mikro MK3 device found.  Please connect the device and try again.");
	return 1;
}
catch (Exception ex)
{
	Console.Error.WriteLine($"Unexpected error: {ex.Message}");
	return 1;
}
finally
{
	TryBlankSurface();
}

return 0;
