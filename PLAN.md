# Maschine.Api — Continuation Plan

> **For the incoming AI**: pick up exactly where the previous session left off.
> Build target: 100% code coverage, A Codacy rating, NugetManagement compliance.

---

## Current state (as of 2026-05-01)

| Check | Status |
|---|---|
| `dotnet build --configuration Release` | ✅ Clean — 0 errors, 0 warnings |
| `dotnet test --configuration Release` | ✅ 97/97 tests pass |
| Line coverage | ⚠️ 84.77% (167/197 lines) |
| Branch coverage | ⚠️ 83.33% |
| Initial git commit | ❌ Not yet committed |
| Codacy badge in README | ❌ Placeholder only |

---

## Repository layout

```
C:\Users\david\source\repos\panoramicdata\Maschine.Api\
├── Maschine.Api\
│   ├── Maschine.Api.csproj
│   ├── MaschineClient.cs          ← main client (see coverage gaps below)
│   ├── MaschinePads.cs
│   ├── MaschineButtons.cs
│   ├── MaschineEncoders.cs
│   ├── Exceptions\
│   ├── Interfaces\
│   ├── Internal\
│   │   ├── MikroMk3Protocol.cs    ← pure static, fully covered
│   │   ├── HidSharpDeviceFactory.cs   [ExcludeFromCodeCoverage]
│   │   ├── HidSharpDevice.cs          [ExcludeFromCodeCoverage]
│   │   └── HidDeviceEnumerator.cs     [ExcludeFromCodeCoverage]
│   └── Models\
├── Maschine.Api.Test\
│   ├── Maschine.Api.Test.csproj
│   └── MaschineClientTests.cs     ← all tests here; uses FakeHidDevice
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── version.json
├── Maschine.Api.slnx
├── README.md
├── CHANGELOG.md
└── .github\workflows\build.yml
```

---

## How to build and test

```powershell
cd C:\Users\david\source\repos\panoramicdata\Maschine.Api

# Build
dotnet build --configuration Release

# Test with coverage
dotnet test --configuration Release --collect:"XPlat Code Coverage"

# Coverage XML location (GUID folder name changes each run):
# Maschine.Api.Test\TestResults\<guid>\coverage.cobertura.xml
```

---

## Priority task list

### 1. Drive coverage to 100% ← START HERE

The coverage gaps are all in `MaschineClient.cs`. Here is the exact list from the last Cobertura report:

#### `MaschineClientLog` (source-generated `[LoggerMessage]` partial class, lines 9–55)

These lines are the compiler-generated partial method implementations. They are hit only when
the corresponding log level is enabled AND the relevant code path executes.

**Root cause**: the three public constructors (lines 46, 55, 65) delegate to the internal
`(options, factory, logger)` constructor, but tests only call the internal constructor
directly. Additionally, log messages for `Connected`, `Disconnected`, `ReadError`, and
`UnhandledReport` are never hit with a real `ILogger` (tests use `NullLogger`).

**Fix options (choose one)**:

- **Option A — `[ExcludeFromCodeCoverage]` on `MaschineClientLog`**: The partial class is
  compiler-generated boilerplate. Applying `[ExcludeFromCodeCoverage]` to the class or its
  individual methods is the most pragmatic fix and does not reduce meaningful test quality.
  Add `[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]` to `MaschineClientLog`.

- **Option B — Test with a recording logger**: Use `Microsoft.Extensions.Logging.Testing`
  (or a hand-rolled `RecordingLogger`) so that log calls actually execute. This gives
  genuine coverage but requires setting up a logger that records messages and verifying them.

#### `MaschineClient` public constructors (lines 46, 48, 55, 57, 65, 67)

Three public constructors are never called from tests (tests use the `internal` constructor).

**Fix**: Add three test cases that call `new MaschineClient()`,
`new MaschineClient(options)`, and `new MaschineClient(options, logger)` and immediately
assert that `ConnectAsync` throws `MaschineDeviceNotFoundException` (because no real device
is attached). The `HidSharpDeviceFactory` returns `null` when no device is found, so the
test will throw predictably without hardware.

> **Important**: `HidSharpDeviceFactory` is decorated `[ExcludeFromCodeCoverage]` so it
> will not appear in the report, but its `TryOpen` returns `null` on a machine without a
> Maschine device, which is the expected behaviour in CI.

#### `DisconnectAsync` (lines 125, 128)

Lines 125–128 are the `OperationCanceledException` catch block inside `DisconnectAsync`.
This path runs when `_readLoop` itself throws `OperationCanceledException`.

**Fix**: Add a test that:
1. Connects with a `FakeHidDevice` that throws `OperationCanceledException` on `ReadAsync`.
2. Calls `DisconnectAsync()`.
3. Asserts it completes without throwing.

#### `RunReadLoopAsync` (lines 161, 170, 172, 173)

- Line 161: `if (report.Length == 0) { continue; }` — empty-report guard not tested.
- Lines 170–173: the `catch (Exception ex)` branch for non-cancellation exceptions from
  `ReadAsync` is not tested.

**Fix**:
- Empty report: enqueue a `byte[0]` from `FakeHidDevice.EnqueueReport`, then enqueue a
  valid report, assert only the valid one fires an event.
- Non-cancellation exception: make `FakeHidDevice.ReadAsync` throw an `IOException` (or
  similar) and assert the read loop exits cleanly (e.g. check that the next read never
  fires after the exception).

---

### 2. Initial git commit and push

```powershell
cd C:\Users\david\source\repos\panoramicdata\Maschine.Api
git init          # if not already done
git add .
git commit -m "Initial commit — 97 tests passing, 84.77% line coverage"
git remote add origin https://github.com/panoramicdata/Maschine.Api.git
git push -u origin main
```

> Check `git status --short` first; as of last run, all files are untracked.

---

### 3. Verify Codacy rating is A

After pushing, visit https://app.codacy.com/ and add the repository.
Once Codacy analyses the first commit, check:
- Overall grade is A.
- No issues flagged by Roslyn/Sonar that would reduce the rating.

Then update the badge URL in `README.md` (currently a placeholder):
```markdown
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/<YOUR_CODACY_TOKEN>)](https://app.codacy.com/gh/panoramicdata/Maschine.Api)
```

---

### 4. NugetManagement compliance check

Run the `nuget` skill to confirm the published package meets PanoramicData standards:

```powershell
.\.github\skills\nuget\Nuget.ps1 -Action assess -PackageName Maschine.Api
```

Key rules to verify:
- `net10.0` target framework ✅
- `<Nullable>enable</Nullable>` ✅
- `<ImplicitUsings>enable</ImplicitUsings>` ✅
- Copyright header in csproj ✅ (verify)
- `TreatWarningsAsErrors=true` ✅
- HTTP-01 (Refit) rule is **explicitly waived** — not applicable to a HID device library.

---

## Key design decisions (do not change without understanding the rationale)

| Decision | Rationale |
|---|---|
| `IHidDevice` / `IHidDeviceFactory` as `internal` interfaces | Keeps the abstraction seam testable without polluting the public API surface. `InternalsVisibleTo` allows the test project to see them. |
| `HidSharpDeviceFactory`, `HidSharpDevice`, `HidDeviceEnumerator` marked `[ExcludeFromCodeCoverage]` | These are thin wrappers around HidSharp; real coverage requires physical hardware. Excluding them avoids CI coverage inflation being gated on hardware availability. |
| `MaschineClientLog` as a partial static class with `[LoggerMessage]` | Required to satisfy CA1848 ("use LoggerMessage delegates for performance"). The class contains compiler-generated code. |
| `FakeHidDevice` in tests uses a `_pending` TCS queue | Allows tests to deliver reports to an already-awaiting `ReadAsync` call. Earlier TCS-free design caused flaky event tests. |
| Central package management (`Directory.Packages.props`) | PanoramicData NugetManagement convention. All package versions live here; `*.csproj` files omit `Version=` attributes. |

---

## How `FakeHidDevice` works (needed to understand the test helper)

`FakeHidDevice` lives in `MaschineClientTests.cs`. It maintains:
- `_queue`: a `Queue<byte[]>` of pre-enqueued reports.
- `_pending`: a `Queue<TaskCompletionSource<byte[]>>` for awaiting `ReadAsync` calls.

When `EnqueueReport(report)` is called:
- If a TCS is already pending (i.e. `ReadAsync` is waiting), the TCS is completed immediately.
- Otherwise the report is placed on `_queue`.

When `ReadAsync` is called:
- If `_queue` has a report ready, it is returned immediately.
- Otherwise a new TCS is created, pushed to `_pending`, and awaited.

This design avoids race conditions and keeps test code synchronous-feeling.

---

## USB constants (for reference)

```csharp
// Maschine Mikro MK3
VendorId  = 0x17CC
ProductId = 0x1700
```

Report IDs (from `MikroMk3Protocol.cs`):
- `0x20` — Pad pressure
- `0x30` — Buttons
- `0x40` — Encoder
