# Maschine.Api

[![NuGet](https://img.shields.io/nuget/v/Maschine.Api)](https://www.nuget.org/packages/Maschine.Api/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/TODO)](https://app.codacy.com/gh/panoramicdata/Maschine.Api/dashboard)

A .NET library for interacting with Native Instruments Maschine controllers over USB HID.
Currently targets the **Maschine Mikro MK3** (VID `0x17CC` / PID `0x1700`).

## Features

- Full HID communication via [HidSharp](https://www.zer7.com/software/hidsharp)
- RGB pad lighting control (single pad or all pads)
- Pad pressure events (12-bit resolution per pad)
- Button state change events (45 buttons, bit-packed)
- Encoder delta events (9 encoders, signed relative rotation)
- Full testability via injected `IHidDeviceFactory` abstraction
- 100% unit test coverage with no hardware required

## Installation

```
dotnet add package Maschine.Api
```

## Quick Start

```csharp
using Maschine.Api;
using Maschine.Api.Models;

await using var client = new MaschineClient();
await client.ConnectAsync();

// Light all pads red
await client.Pads.SetAllColorsAsync(PadColor.Red);

// React to pad presses
client.Pads.PadChanged += (_, pad) =>
{
    if (pad.IsPressed)
        Console.WriteLine($"Pad {pad.Index} pressed with pressure {pad.Pressure}");
};

// React to button presses
client.Buttons.ButtonChanged += (_, button) =>
    Console.WriteLine($"Button {button.Index} {(button.IsPressed ? "down" : "up")}");

// React to encoder rotation
client.Encoders.EncoderChanged += (_, encoder) =>
    Console.WriteLine($"Encoder {encoder.Index} moved by {encoder.Delta}");

Console.ReadLine();
await client.DisconnectAsync();
```

## Device Support

| Device              | VID    | PID    | Status |
|---------------------|--------|--------|--------|
| Maschine Mikro MK3  | 0x17CC | 0x1700 | ✓      |

## Requirements

- .NET 10.0 or later
- Windows, macOS, or Linux (HidSharp cross-platform)
- Native Instruments USB driver (Windows) or libusb/hidraw (Linux)

## License

MIT — see [LICENSE](LICENSE) for details.
