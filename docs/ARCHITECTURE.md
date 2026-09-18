# Architecture

```
                     ┌─────────────────────────┐
                     │   WinDivert driver       │  (kernel, prebuilt & signed)
                     │   intercepts IP packets  │
                     └────────────┬─────────────┘
                                  │ WinDivertRecv / WinDivertSend
                     ┌────────────▼─────────────┐
                     │ WinDivertPacketDiverter   │  Core/Diversion
                     │ (IPacketDiverter impl)    │
                     └────────────┬─────────────┘
                                  │ CapturedPacket
                     ┌────────────▼─────────────┐        ┌───────────────────────┐
                     │   ThrottlePipeline        │◄──────►│ ProcessPortLookup      │
                     │  - resolve PID for packet │        │ (GetExtendedTcp/UdpTable)
                     │  - ask RuleEngine for a    │        └───────────────────────┘
                     │    TokenBucket             │
                     │  - consume or delay+queue  │
                     └────────────┬─────────────┘
                                  │
                     ┌────────────▼─────────────┐        ┌───────────────────────┐
                     │      RuleEngine           │◄──────►│    RuleStore (JSON)   │
                     │  BandwidthRule -> bucket   │        │ %APPDATA%/.../rules.json
                     └────────────┬─────────────┘        └───────────────────────┘
                                  │
                     ┌────────────▼─────────────┐
                     │      TokenBucket           │  bytes/sec cap per (pid, direction)
                     └───────────────────────────┘

                     ┌───────────────────────────┐
                     │   NetLimiterLite.App (WPF) │  process list + limit sliders,
                     │   MainWindow.xaml(.cs)     │  reads/writes rules via RuleEngine/RuleStore
                     └───────────────────────────┘
```

## Why delay instead of drop

A hard packet drop under TCP triggers retransmission + congestion-control backoff, which produces
bursty, laggy behavior — exactly what you don't want for a game running alongside a throttled
download. Delaying re-injection (queueing until the token bucket has enough budget) instead makes
the throttled flow behave like it's on a slower physical link: smooth and predictable. UDP-based
game traffic is usually *not* the thing you throttle — you throttle the other process (e.g. a
downloader/game client patcher) so the actual game gets a bigger effective share of the pipe.

## Why not touch WFP directly

NetLimiter's real implementation uses a WFP callout driver, which needs:
- A kernel-mode driver project (C, WDK toolchain).
- An EV code-signing certificate (or test-signing mode, which most users won't enable).
- Careful handling to avoid BSODs — any bug in a WFP callout runs in the network stack's hot path.

WinDivert already solved this and ships a signed driver; building on top of it gets ~95% of the
practical benefit (accurate per-process, per-connection shaping) with none of the driver
development/signing cost. This is a legitimate trade worth naming explicitly rather than silently
"cheating" — if true WFP-level integration (e.g. for a commercial product needing tighter OS
integration) is ever required, the `IPacketDiverter` abstraction is the seam where a
WFP-backed implementation could be swapped in later without touching `RuleEngine`/`TokenBucket`.
