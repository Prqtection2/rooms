namespace Rooms.Application.Switching;

/// <summary>
/// Guards against feedback loops (§6.2): hiding/showing windows fires WinEvents, which would
/// otherwise make the RuleEngine react to the switch's own actions. The orchestrator opens a
/// batch around its window operations; reactive services skip work while a batch is in progress.
/// </summary>
public interface ISwitchGate
{
    bool IsBatching { get; }

    IDisposable Begin();
}

/// <inheritdoc cref="ISwitchGate" />
public sealed class SwitchGate : ISwitchGate
{
    private int _depth;

    public bool IsBatching => Volatile.Read(ref _depth) > 0;

    public IDisposable Begin()
    {
        Interlocked.Increment(ref _depth);
        return new Scope(this);
    }

    private void End() => Interlocked.Decrement(ref _depth);

    private sealed class Scope(SwitchGate gate) : IDisposable
    {
        private SwitchGate? _gate = gate;

        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.End();
    }
}
