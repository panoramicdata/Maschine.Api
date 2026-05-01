using Maschine.Api.Exceptions;
using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
}

/// <summary>
/// Main client for interacting with a connected Maschine Mikro MK3 controller.
/// </summary>
public sealed class MaschineClient : IMaschineClient
{
private readonly MaschineClientOptions _options;
private readonly IHidDeviceFactory _factory;
private readonly ILogger<MaschineClient> _logger;
private IHidDevice? _device;
private MaschinePads? _pads;
private MaschineButtons? _buttons;
private MaschineEncoders? _encoders;
private MikroMk3UnifiedLights? _unifiedLights;
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
}

/// <inheritdoc/>
public IPads Pads => EnsureConnected(_pads);

/// <inheritdoc/>
public IButtons Buttons => EnsureConnected(_buttons);

/// <inheritdoc/>
public IEncoders Encoders => EnsureConnected(_encoders);

/// <inheritdoc/>
public Task ConnectAsync(CancellationToken cancellationToken = default)
{
ObjectDisposedException.ThrowIf(_disposed, this);

_device = _factory.TryOpen(_options.VendorId, _options.ProductId, _options.DeviceIndex)
?? throw new MaschineDeviceNotFoundException(_options.VendorId, _options.ProductId);

		_unifiedLights = new MikroMk3UnifiedLights(_device);
		_pads = new MaschinePads(_device, _unifiedLights);
		_buttons = new MaschineButtons(_device, _unifiedLights);
_encoders = new MaschineEncoders();

_readLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
_readLoop = RunReadLoopAsync(_readLoopCts.Token);

MaschineClientLog.Connected(_logger, _options.VendorId, _options.ProductId);

return Task.CompletedTask;
}

/// <inheritdoc/>
public async Task DisconnectAsync()
{
if (_readLoopCts is not null)
{
await _readLoopCts.CancelAsync().ConfigureAwait(false);
}

if (_readLoop is not null)
{
try
{
await _readLoop.ConfigureAwait(false);
}
catch (OperationCanceledException)
{
// Expected on disconnect.
}
}

_device?.Dispose();
_device = null;
_unifiedLights?.Dispose();
_unifiedLights = null;
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

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // null branches on ?. are unreachable: fields are always set before DispatchReport is called
private void DispatchReport(byte[] report)
{
switch (report[0])
{
case MikroMk3Protocol.PadPressureReportId:
_pads?.ApplyReport(report);
break;

case MikroMk3Protocol.ButtonReportId:
_buttons?.ApplyReport(report);
break;

case MikroMk3Protocol.EncoderReportId:
_encoders?.ApplyReport(report);
break;

default:
MaschineClientLog.UnhandledReport(_logger, report[0]);
break;
}
}

private static T EnsureConnected<T>(T? value) where T : class
=> value ?? throw new InvalidOperationException(
"Not connected. Call ConnectAsync() before accessing device features.");
}
