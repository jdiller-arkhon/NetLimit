# NetLimiterLite

A lightweight, NetLimiter-style per-process bandwidth monitor and throttler for Windows, aimed at
gamers who want to cap/prioritize bandwidth for specific processes (e.g. throttle a background
downloader while a game is running).

## Why WinDivert instead of a custom WFP driver

Real per-process, per-connection traffic shaping on Windows requires intercepting packets below
the socket layer. There are two ways to do that:

1. **Windows Filtering Platform (WFP)** — the approach NetLimiter itself uses. Requires writing
   and code-signing your own kernel-mode callout driver. This can't be built or tested in this
   sandbox (no Windows kernel toolchain, no code-signing cert), and even on a real machine it's a
   multi-week undertaking with a steep debugging cost (BSODs on mistakes).
2. **[WinDivert](https://reqrypt.org/windivert.html)** — a mature, already-signed Windows Packet
   Divert driver with a simple user-mode API. You install their driver once (no custom signing
   needed) and filter/delay/drop packets from ordinary user-mode code. This is what tools like
   NetBalancer and various game-boost utilities use in practice.

This project uses WinDivert. That means:
- No custom kernel driver to write, sign, or ship.
- Everything here is buildable/testable in normal C# tooling.
- **Caveat:** you still need to run the WinDivert driver install + this app **as Administrator on
  a real Windows machine** — it cannot run or be tested inside this Linux sandbox. This repo is
  the full design + scaffold; final build/run/signing of the installer happens on your machine.

## Architecture

```
NetLimiterLite.sln
  src/
    NetLimiterLite.Core/       - platform-agnostic logic (net8.0)
      Netstat/                 - TCP/UDP table -> PID mapping (GetExtendedTcpTable/UdpTable)
      Diversion/               - WinDivert wrapper: packet capture, per-flow bucketing
      Throttling/              - token-bucket rate limiter, per-process rules
      Model/                   - ProcessFlow, BandwidthRule, LiveStats
    NetLimiterLite.App/        - WPF UI (net8.0-windows), tray icon, process list, sliders
    NetLimiterLite.Tests/      - unit tests for rate limiter / rule matching (no WinDivert needed)
  docs/
    ARCHITECTURE.md
    WINDOWS_SETUP.md
```

## Core design

- **Packet interception**: open a WinDivert handle with filter `outbound or inbound` (or split
  in/out into two handles for accurate direction accounting). Every captured packet is parsed for
  its 5-tuple (proto, local/remote addr+port).
- **Process attribution**: on each capture, look up the owning PID via
  `GetExtendedTcpTable`/`GetExtendedUdpTable`, cached and refreshed periodically (connections are
  short-lived; a 1-2s cache TTL is enough since games use long-lived sockets).
- **Rate limiting**: token-bucket per (PID, direction). If a bucket has tokens, packet is
  re-injected immediately (`WinDivertSend`); otherwise it's queued and released on a timer tied to
  the configured rate. This gives smooth throttling instead of hard packet drops (which would look
  like packet loss to a game).
- **Rules**: user sets a rate cap (KB/s) or priority tier per process name/path. Rules persist to a
  local JSON config (`%APPDATA%/NetLimiterLite/rules.json`).
- **UI**: live list of processes with active sockets, current up/down throughput, and a slider/text
  box per process for the cap. System tray icon for quick pause/resume of all throttling.

## Status

This is a scaffold: project structure, interfaces, and the rate-limiter/rule-matching logic (which
needs no Windows-specific APIs) are implemented and unit-tested here. The WinDivert P/Invoke layer
and WPF UI are stubbed with clear TODOs — they need a Windows machine with the WinDivert SDK to
build and test.

See `docs/WINDOWS_SETUP.md` for exact next steps to finish and run this on Windows.
