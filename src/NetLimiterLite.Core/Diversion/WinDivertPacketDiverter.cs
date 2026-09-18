using System.Runtime.Versioning;

namespace NetLimiterLite.Core.Diversion;

/// <summary>
/// TODO(windows-machine): Real WinDivert-backed implementation. Not buildable/testable in this
/// sandbox (no Windows, no WinDivert.dll/.sys). Left as a documented stub with the exact P/Invoke
/// surface needed so this can be filled in quickly on a Windows dev box.
///
/// Steps to complete this on Windows:
///   1. Download the WinDivert SDK (https://reqrypt.org/windivert.html) - it ships WinDivert.dll,
///      WinDivert32.sys/WinDivert64.sys (kernel driver, already signed by the WinDivert authors),
///      and a C header.
///   2. Add WinDivert.dll next to the app output, and either the .sys drivers alongside it or
///      install them via `WinDivert.dll`'s auto-install on first `WinDivertOpen` call (it will
///      install the driver service using the .sys file found next to the DLL).
///   3. P/Invoke the four functions actually needed here:
///        HANDLE WinDivertOpen(const char *filter, WINDIVERT_LAYER layer, INT16 priority, UINT64 flags);
///        BOOL   WinDivertRecv(HANDLE handle, VOID *pPacket, UINT packetLen, UINT *pRecvLen, WINDIVERT_ADDRESS *pAddr);
///        BOOL   WinDivertSend(HANDLE handle, const VOID *pPacket, UINT packetLen, UINT *pSendLen, const WINDIVERT_ADDRESS *pAddr);
///        BOOL   WinDivertClose(HANDLE handle);
///      A filter of "outbound or inbound" at WINDIVERT_LAYER_NETWORK captures all IP traffic;
///      narrow it to "tcp or udp" to skip ICMP etc.
///   4. Use WinDivertHelperParsePacket to pull out the IPv4/IPv6 header + TCP/UDP header pointers
///      so you can populate CapturedPacket's address/port fields without hand-rolling a parser.
///   5. For throttling: on Receive(), look up the owning PID via ProcessPortLookup, ask
///      RuleEngine for a TokenBucket for (pid, direction). If TryConsume succeeds, call
///      Reinject() immediately. If not, don't drop the packet - queue it (e.g. in a
///      PriorityQueue keyed by "release time") and have a background timer thread call
///      Reinject() once the bucket has tokens. This is what makes throttling look like a slower
///      pipe instead of packet loss/jitter.
///   6. WinDivertSend requires recalculated checksums if you mutate the packet; if you're only
///      delaying (not modifying) the payload, the original checksums remain valid.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WinDivertPacketDiverter : IPacketDiverter
{
    public void Start()
    {
        throw new PlatformNotSupportedException(
            "WinDivertPacketDiverter requires the WinDivert driver and can only run on Windows. " +
            "See the class doc comment for the exact P/Invoke surface to implement.");
    }

    public void Stop() { }

    public CapturedPacket? Receive() => throw new NotImplementedException();

    public void Reinject(CapturedPacket packet) => throw new NotImplementedException();

    public void Dispose() { }
}
