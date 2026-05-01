# Maschine.Api Design

## Purpose

`Maschine.Api` is a .NET 10 library for controlling and reading Native Instruments Maschine hardware over USB HID, currently focused on Maschine Mikro MK3 (`VID 0x17CC`, `PID 0x1700`).

The library exposes a small public API (`IMaschineClient`, `IPads`, `IButtons`, `IEncoders`) and keeps HID transport details behind internal abstractions for testability.

## Solution Structure

- `Maschine.Api/`: shipping library package
- `Maschine.Api.Test/`: unit tests (xUnit v3)
- `Maschine.Demo/`: interactive hardware demo app

## Architectural Overview

### 1) Client and Lifecycle

`MaschineClient` is the central orchestrator:

- Opens a HID device via `IHidDeviceFactory`
- Creates feature modules (`MaschinePads`, `MaschineButtons`, `MaschineEncoders`)
- Starts a background read loop (`RunReadLoopAsync`) that dispatches HID input reports
- Exposes typed module interfaces through `Pads`, `Buttons`, and `Encoders`

Lifecycle methods:

- `ConnectAsync(...)`: opens device, initializes modules, starts read loop
- `DisconnectAsync()`: cancels read loop, awaits loop completion, disposes hardware resources
- `Dispose()`: cancellation/disposal safety net

### 2) HID Abstraction Boundary

Internal interfaces isolate hardware access:

- `IHidDevice`: read/write/report operations
- `IHidDeviceFactory`: open device by VID/PID/index

Production implementations (`HidSharpDevice`, `HidSharpDeviceFactory`) wrap HidSharp and are excluded from coverage because hardware access is environment-dependent.

### 3) Protocol Layer

`MikroMk3Protocol` is a static pure-function codec layer:

- Input report parse:
  - pad pressure (`0x20`)
  - button state (`0x01`)
  - encoder delta (`0x02`)
- Output report build:
  - pad LED report (`0x80`)
  - button LED report (`0x81`)

This separation keeps binary protocol rules deterministic and unit-testable.

### 4) Feature Modules

- `MaschinePads`:
  - tracks 16 pad pressure states
  - emits `PadChanged`
  - sets single/all pad colors
- `MaschineButtons`:
  - tracks 45 button states
  - emits `ButtonChanged`
  - sets single/all button LED brightness
- `MaschineEncoders`:
  - emits `EncoderChanged` for non-zero deltas (9 encoders)

### 5) Lighting Compatibility Strategy

`MikroMk3UnifiedLights` maintains an internal 81-byte unified light packet and writes incremental updates under a semaphore to avoid concurrent packet mutation.

Pads/buttons first try direct LED report writes; on known unsupported HID feature errors, modules switch to unified packet mode as fallback. This gives compatibility across host/device-driver behavior differences.

### 6) Concurrency and Events

- A background read loop continuously consumes input reports.
- Report dispatch updates internal module state and raises events only on change.
- Cancellation token linked source is used to stop read loop during disconnect.
- `MikroMk3UnifiedLights` serializes writes via `SemaphoreSlim`.

## Build and Validation (Current Session)

Commands executed at repo root:

- `dotnet build --configuration Release`
- `dotnet test --configuration Release`

Observed outcomes:

- Build initially failed because `Maschine.Demo` process held a lock on `Maschine.Api.dll` (MSB3021/MSB3027).
- After stopping the running demo process, root build succeeded.
- Root tests passed: 118 passed, 0 failed.

## Code Review Findings

Ordered by severity.

1. Medium: `MaschineClient.ConnectAsync` is re-entrant without a connected-state guard.

- Location: `Maschine.Api/MaschineClient.cs` (`ConnectAsync`)
- Risk: calling `ConnectAsync` twice can replace `_device`/module references and start another read loop without a coordinated teardown path. This can leak resources and produce undefined event behavior.
- Recommendation: enforce single active connection (throw `InvalidOperationException` when already connected), or auto-disconnect before reconnecting.

2. Low: root build is operationally fragile while demo is running.

- Location: solution-level behavior involving `Maschine.Demo`
- Risk: running `Maschine.Demo` can lock output assemblies and cause root build failures (copy retries then MSB3021/MSB3027).
- Recommendation: document this in developer guidance and optionally adjust workflow scripts to stop running demo process before full-solution build.

3. Low: README quick-start snippet likely does not match the current disposal contract.

- Location: `README.md` (`await using var client = new MaschineClient();`)
- Risk: `MaschineClient` implements `IDisposable`, not `IAsyncDisposable`; the snippet can fail for consumers as written.
- Recommendation: change snippet to `using var client = new MaschineClient();` unless async disposal is intentionally implemented.

## Testing Posture

Strengths:

- Protocol parsing/building is structured for pure unit tests.
- HID transport is abstracted, enabling hardware-free tests.
- Event-driven modules are testable via fake device data.

Gap to monitor:

- Hardware wrappers are intentionally excluded from coverage; integration behavior still depends on live device validation.

## Non-Goals / Current Scope

- Only Mikro MK3 constants and protocol are implemented.
- Multi-device orchestration beyond index selection is not in current API surface.

## Suggested Next Design Iterations

1. Add explicit connection state machine (`Disconnected`, `Connecting`, `Connected`, `Disconnecting`) and enforce legal transitions.
2. Decide whether `MaschineClient` should support `IAsyncDisposable` for symmetry with async lifecycle operations.
3. Optionally expose diagnostics counters (reports read, parse failures, dropped writes) for production troubleshooting.
4. Add integration smoke tests gated by environment variable when hardware is available.
