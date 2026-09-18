namespace NetLimiterLite.Core.Model;

public enum FlowDirection
{
    Outbound,
    Inbound,
}

public enum FlowProtocol
{
    Tcp,
    Udp,
}

/// <summary>
/// Identifies a single network flow (5-tuple) attributed to an owning process.
/// </summary>
public sealed record FlowKey(
    FlowProtocol Protocol,
    string LocalAddress,
    int LocalPort,
    string RemoteAddress,
    int RemotePort)
{
    public static FlowKey ForOutbound(FlowProtocol proto, string localAddr, int localPort, string remoteAddr, int remotePort)
        => new(proto, localAddr, localPort, remoteAddr, remotePort);
}

/// <summary>
/// A process currently observed to own one or more flows, with aggregated live throughput.
/// </summary>
public sealed class ProcessFlow
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public string? ExecutablePath { get; init; }

    public long BytesSentPerSecond { get; set; }
    public long BytesReceivedPerSecond { get; set; }

    public long TotalBytesSent { get; set; }
    public long TotalBytesReceived { get; set; }
}
