namespace NetLimiterLite.Core.Diversion;

public sealed class CapturedPacket
{
    public required byte[] Data { get; init; }
    public required bool IsOutbound { get; init; }
    public required bool IsTcp { get; init; }
    public required string LocalAddress { get; init; }
    public required int LocalPort { get; init; }
    public required string RemoteAddress { get; init; }
    public required int RemotePort { get; init; }
}

/// <summary>
/// Abstraction over the packet capture/injection backend so RuleEngine/TokenBucket (and their
/// tests) never depend on WinDivert directly. WinDivertPacketDiverter is the real Windows
/// implementation; a fake implementation can be swapped in for tests.
/// </summary>
public interface IPacketDiverter : IDisposable
{
    /// <summary>Blocks until a packet is captured or the diverter is closed.</summary>
    CapturedPacket? Receive();

    /// <summary>Re-injects a previously captured (and possibly delayed) packet.</summary>
    void Reinject(CapturedPacket packet);

    void Start();
    void Stop();
}
