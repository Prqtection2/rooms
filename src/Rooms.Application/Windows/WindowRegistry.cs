using Rooms.Core.Models;

namespace Rooms.Application.Windows;

/// <summary>How a window came to belong to a room (§6.1).</summary>
public enum AssignmentSource
{
    Unassigned,
    MatchedByRule,
    LaunchedByRoom,
    ManualOverride,
}

/// <summary>A window's current room assignment.</summary>
public sealed record WindowAssignment(IntPtr Handle, Guid? RoomId, AssignmentSource Source);

/// <summary>
/// Live, in-memory map of window ownership (§6.1). Tracks windows launched by a room (by
/// pid), manually assigned by the user, adopted under the current-room policy, and sticky
/// (global) windows. Rule-based matching is evaluated by the RuleEngine, not stored here.
/// </summary>
public interface IWindowRegistry
{
    UnassignedWindowPolicy UnassignedPolicy { get; set; }

    /// <summary>Record that a room launched a process, so its windows are owned on arrival.</summary>
    void TrackLaunch(int pid, Guid roomId);

    /// <summary>User explicitly assigns a window to a room (overrides launch + rules).</summary>
    void AssignManual(IntPtr handle, Guid roomId);

    /// <summary>Adopt an otherwise-unassigned window into a room (current-room policy).</summary>
    void Adopt(IntPtr handle, Guid roomId);

    void SetSticky(IntPtr handle, bool sticky);

    bool IsSticky(IntPtr handle);

    /// <summary>Forget all assignments for a destroyed window.</summary>
    void Forget(IntPtr handle);

    /// <summary>Drop the session-scoped assignments (launched + adopted) for a room, so a Reset
    /// returns it to just its persistent defaults. Manual assignments are kept.</summary>
    void ClearRuntimeAssignments(Guid roomId);

    /// <summary>The room this window is explicitly assigned to (manual &gt; launched &gt; adopted),
    /// or null if only rule-matching (or nothing) applies.</summary>
    Guid? GetExplicitRoom(WindowInfo window);

    AssignmentSource GetSource(WindowInfo window);
}

/// <inheritdoc cref="IWindowRegistry" />
public sealed class WindowRegistry : IWindowRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<IntPtr, Guid> _manual = new();
    private readonly Dictionary<IntPtr, Guid> _adopted = new();
    private readonly Dictionary<int, Guid> _launchedPids = new();
    private readonly HashSet<IntPtr> _sticky = new();

    public UnassignedWindowPolicy UnassignedPolicy { get; set; } = UnassignedWindowPolicy.Sticky;

    public void TrackLaunch(int pid, Guid roomId)
    {
        lock (_lock)
            _launchedPids[pid] = roomId;
    }

    public void AssignManual(IntPtr handle, Guid roomId)
    {
        lock (_lock)
        {
            _manual[handle] = roomId;
            _adopted.Remove(handle);
        }
    }

    public void Adopt(IntPtr handle, Guid roomId)
    {
        lock (_lock)
        {
            if (!_manual.ContainsKey(handle))
                _adopted[handle] = roomId;
        }
    }

    public void SetSticky(IntPtr handle, bool sticky)
    {
        lock (_lock)
        {
            if (sticky)
                _sticky.Add(handle);
            else
                _sticky.Remove(handle);
        }
    }

    public bool IsSticky(IntPtr handle)
    {
        lock (_lock)
            return _sticky.Contains(handle);
    }

    public void Forget(IntPtr handle)
    {
        lock (_lock)
        {
            _manual.Remove(handle);
            _adopted.Remove(handle);
            _sticky.Remove(handle);
        }
    }

    public void ClearRuntimeAssignments(Guid roomId)
    {
        lock (_lock)
        {
            foreach (var handle in _adopted.Where(kv => kv.Value == roomId).Select(kv => kv.Key).ToList())
                _adopted.Remove(handle);

            foreach (var pid in _launchedPids.Where(kv => kv.Value == roomId).Select(kv => kv.Key).ToList())
                _launchedPids.Remove(pid);
        }
    }

    public Guid? GetExplicitRoom(WindowInfo window)
    {
        lock (_lock)
        {
            if (_manual.TryGetValue(window.Handle, out var manual))
                return manual;
            if (_launchedPids.TryGetValue(window.ProcessId, out var launched))
                return launched;
            if (_adopted.TryGetValue(window.Handle, out var adopted))
                return adopted;
            return null;
        }
    }

    public AssignmentSource GetSource(WindowInfo window)
    {
        lock (_lock)
        {
            if (_manual.ContainsKey(window.Handle))
                return AssignmentSource.ManualOverride;
            if (_launchedPids.ContainsKey(window.ProcessId))
                return AssignmentSource.LaunchedByRoom;
            if (_adopted.ContainsKey(window.Handle))
                return AssignmentSource.MatchedByRule;
            return AssignmentSource.Unassigned;
        }
    }
}
