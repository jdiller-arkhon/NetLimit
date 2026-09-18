using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NetLimiterLite.Core.Netstat;

public readonly record struct PortOwner(int ProcessId);

/// <summary>
/// Resolves which process owns a given local (proto, address, port), backed by
/// GetExtendedTcpTable / GetExtendedUdpTable. Results are cached briefly since these syscalls
/// enumerate the whole system table and are too expensive to call per-packet.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ProcessPortLookup
{
    private readonly TimeSpan _cacheTtl;
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private readonly Dictionary<(bool isTcp, int localPort), int> _table = new();
    private readonly object _lock = new();

    public ProcessPortLookup(TimeSpan? cacheTtl = null)
    {
        _cacheTtl = cacheTtl ?? TimeSpan.FromSeconds(1.5);
    }

    public int? FindOwningPid(bool isTcp, int localPort)
    {
        lock (_lock)
        {
            RefreshIfStale();
            return _table.TryGetValue((isTcp, localPort), out var pid) ? pid : null;
        }
    }

    private void RefreshIfStale()
    {
        var now = DateTime.UtcNow;
        if (now - _lastRefreshUtc < _cacheTtl) return;

        _table.Clear();
        foreach (var (port, pid) in ReadTcpTable()) _table[(true, port)] = pid;
        foreach (var (port, pid) in ReadUdpTable()) _table[(false, port)] = pid;

        _lastRefreshUtc = now;
    }

    // --- P/Invoke plumbing -------------------------------------------------
    // NOTE: this needs the Windows iphlpapi.dll and only runs on Windows. The struct layouts
    // below match MIB_TCPROW_OWNER_PID / MIB_TCPTABLE_OWNER_PID (and UDP equivalents) from
    // <iprtrmib.h>. Kept here as a single self-contained file so it's easy to unit test against
    // a fake table on non-Windows by swapping ReadTcpTable/ReadUdpTable behind an interface if
    // needed later.

    private static IEnumerable<(int port, int pid)> ReadTcpTable()
    {
        const int AF_INET = 2;
        int bufferSize = 0;
        _ = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, false, AF_INET, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0);

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            var result = GetExtendedTcpTable(buffer, ref bufferSize, false, AF_INET, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0);
            if (result != 0) yield break;

            var rowCount = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();

            for (var i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(IntPtr.Add(rowPtr, i * rowSize));
                var localPort = ((row.localPort & 0xFF) << 8) | ((row.localPort >> 8) & 0xFF);
                yield return (localPort, row.owningPid);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IEnumerable<(int port, int pid)> ReadUdpTable()
    {
        const int AF_INET = 2;
        int bufferSize = 0;
        _ = GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, false, AF_INET, UdpTableClass.UDP_TABLE_OWNER_PID, 0);

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            var result = GetExtendedUdpTable(buffer, ref bufferSize, false, AF_INET, UdpTableClass.UDP_TABLE_OWNER_PID, 0);
            if (result != 0) yield break;

            var rowCount = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibUdpRowOwnerPid>();

            for (var i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MibUdpRowOwnerPid>(IntPtr.Add(rowPtr, i * rowSize));
                var localPort = ((row.localPort & 0xFF) << 8) | ((row.localPort >> 8) & 0xFF);
                yield return (localPort, row.owningPid);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public int state;
        public int localAddr;
        public int localPort;
        public int remoteAddr;
        public int remotePort;
        public int owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibUdpRowOwnerPid
    {
        public int localAddr;
        public int localPort;
        public int owningPid;
    }

    private enum TcpTableClass
    {
        TCP_TABLE_OWNER_PID_ALL = 5,
    }

    private enum UdpTableClass
    {
        UDP_TABLE_OWNER_PID = 1,
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int size, bool sort, int ipVersion, TcpTableClass tableClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr udpTable, ref int size, bool sort, int ipVersion, UdpTableClass tableClass, uint reserved);
}
