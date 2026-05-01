using Maschine.Api;
using Maschine.Api.Models;
using Maschine.Demo;
using Maschine.Api.Exceptions;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	cts.Cancel();
};

var options = new MaschineClientOptions();
using var client = new MaschineClient(options);
await using var demo = new DemoController(client);

var runLedSelfTest = args.Any(a =>
	a.Equals("--led-test", StringComparison.OrdinalIgnoreCase)
	|| a.Equals("--self-test", StringComparison.OrdinalIgnoreCase));

try
{
	if (runLedSelfTest)
	{
		Console.WriteLine("LED self-test mode enabled.");
	}

	await demo.RunAsync(cts.Token, runLedSelfTest);
}
catch (MaschineDeviceNotFoundException)
{
	Console.Error.WriteLine("No Maschine Mikro MK3 device found.  Please connect the device and try again.");
	return 1;
}

return 0;
