using System.Collections.Concurrent;
using NetLimiterLite.Core.Diversion;

namespace NetLimiterLite.Core.Throttling;

/// <summary>
/// Wires an IPacketDiverter to the RuleEngine: for every captured packet, resolves the owning
/// process, checks the relevant token bucket, and either re-injects immediately or schedules a
/// delayed re-injection. Delaying (rather than dropping) keeps throttled connections looking like
/// a slow link instead of a lossy one, which games handle far more gracefully.
/// </summary>
public sealed class ThrottlePipeline : IDisposable
{
    private readonly IPacketDiverter _diverter;
    private readonly RuleEngine _rules;
    private readonly Func<bool, int, int?> _resolvePid; // (isTcp, localPort) -> pid
    private readonly Func<int, (string name, string? path)?> _resolveProcessInfo;

    private readonly ConcurrentQueue<(DateTime releaseAtUtc, CapturedPacket packet)> _delayed = new();
    private readonly Timer _releaseTimer;
    private CancellationTokenSource? _cts;
    private Thread? _receiveThread;

    public ThrottlePipeline(
        IPacketDiverter diverter,
        RuleEngine rules,
        Func<bool, int, int?> resolvePid,
        Func<int, (string name, string? path)?> resolveProcessInfo)
    {
        _diverter = diverter;
        _rules = rules;
        _resolvePid = resolvePid;
        _resolveProcessInfo = resolveProcessInfo;
        _releaseTimer = new Timer(_ => ReleaseDuePackets(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _diverter.Start();
        _releaseTimer.Change(0, 20); // check for due packets every 20ms

        _receiveThread = new Thread(() => ReceiveLoop(_cts.Token)) { IsBackground = true };
        _receiveThread.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _diverter.Stop();
        _releaseTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void ReceiveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var packet = _diverter.Receive();
            if (packet is null) continue;

            HandlePacket(packet);
        }
    }

    private void HandlePacket(CapturedPacket packet)
    {
        var localPort = packet.LocalPort;
        var pid = _resolvePid(packet.IsTcp, localPort);

        // Unattributed traffic (couldn't map to a PID in time) passes through unthrottled rather
        // than being held indefinitely.
        if (pid is null)
        {
            _diverter.Reinject(packet);
            return;
        }

        var info = _resolveProcessInfo(pid.Value);
        var isDownload = !packet.IsOutbound;
        var bucket = _rules.GetBucket(pid.Value, info?.name ?? string.Empty, info?.path, isDownload);

        if (bucket is null || bucket.TryConsume(packet.Data.Length, out var wait) && wait == TimeSpan.Zero)
        {
            _diverter.Reinject(packet);
            return;
        }

        if (!bucket.TryConsume(packet.Data.Length, out var retryAfter))
        {
            _delayed.Enqueue((DateTime.UtcNow + retryAfter, packet));
            return;
        }

        _diverter.Reinject(packet);
    }

    private void ReleaseDuePackets()
    {
        var now = DateTime.UtcNow;
        var requeue = new List<(DateTime, CapturedPacket)>();

        while (_delayed.TryDequeue(out var item))
        {
            if (item.releaseAtUtc <= now)
            {
                _diverter.Reinject(item.packet);
            }
            else
            {
                requeue.Add(item);
            }
        }

        foreach (var item in requeue) _delayed.Enqueue(item);
    }

    public void Dispose()
    {
        Stop();
        _releaseTimer.Dispose();
        _diverter.Dispose();
    }
}
