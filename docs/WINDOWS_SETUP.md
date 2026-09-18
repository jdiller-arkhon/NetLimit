# Finishing this on Windows

This repo was scaffolded and designed outside Windows, so the parts that need real Windows APIs
(WinDivert packet capture, the WPF app running as Administrator) are stubbed with clear `TODO`
comments. Here's the exact path to a working build.

## Prerequisites

1. Windows 10/11, .NET 8 SDK.
2. [WinDivert](https://reqrypt.org/windivert.html) — download the SDK zip. It contains:
   - `WinDivert.dll` (x64/x86) — the user-mode API you P/Invoke.
   - `WinDivert64.sys` / `WinDivert32.sys` — the kernel driver, **already signed** by the
     WinDivert project. You do not need your own code-signing certificate.
3. Visual Studio 2022 (or `dotnet build`/`dotnet run` from the CLI) is enough; no separate driver
   build tooling needed since you're consuming the prebuilt driver.

## Steps

1. `git clone` this repo, open `NetLimiterLite.sln`.
2. Copy `WinDivert.dll` and the matching `WinDivert64.sys`/`WinDivert32.sys` into
   `src/NetLimiterLite.App/` (or set them to copy-to-output in the `.csproj`). The first
   `WinDivertOpen` call auto-installs the driver service using the `.sys` file it finds next to
   the DLL.
3. Implement `WinDivertPacketDiverter` (`src/NetLimiterLite.Core/Diversion/WinDivertPacketDiverter.cs`)
   — the class doc comment has the exact 4 functions to P/Invoke and the parsing helper to use.
   This is the only piece of real "new code" left; everything it plugs into (`ThrottlePipeline`,
   `RuleEngine`, `TokenBucket`, `ProcessPortLookup`) is already implemented and unit-tested.
4. Wire it up in `MainWindow.xaml.cs` where the `TODO(windows-machine)` comments are: construct a
   `WinDivertPacketDiverter`, a `ProcessPortLookup`, and a `ThrottlePipeline`, `Start()` it, and
   poll process/connection stats on a `DispatcherTimer` (e.g. every 500ms) to update `Rows`.
5. Build and run **as Administrator** — WinDivert requires elevated privileges. The app manifest
   (`app.manifest`) already requests this via UAC.
6. `dotnet test src/NetLimiterLite.Tests` runs the platform-agnostic logic tests (token bucket,
   rule matching) without needing WinDivert or admin rights at all — good for CI on any OS.

## Testing without a real game

- Use a bandwidth-heavy but harmless test target, e.g. an `iperf3` server, or just download a
  large file with a browser/curl, and confirm the throughput in NetLimiterLite's UI matches
  Task Manager's per-process network column before/after applying a limit.
- Verify the "don't drop, delay" behavior: run `ping` against a host while throttling that
  process's traffic — you should see increased latency, not packet loss, as the cap tightens.

## Known gaps / next steps

- IPv6 isn't handled in `ProcessPortLookup` yet (only `AF_INET`); add `GetExtendedTcpTable`/
  `GetExtendedUdpTable` calls with `AF_INET6` and matching struct layouts if you need it.
- No installer yet (e.g. no MSI/Inno Setup script bundling WinDivert + the app) — packaging is a
  follow-up once the core loop is verified.
- Per-connection prioritization (vs. flat per-process rate caps) isn't modeled; `BandwidthRule`
  would need a `Priority` field and `ThrottlePipeline` would need to weight bucket refills across
  processes instead of treating each independently.
