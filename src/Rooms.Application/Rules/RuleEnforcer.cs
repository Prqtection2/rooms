using Microsoft.Extensions.Logging;
using Rooms.Application.Switching;
using Rooms.Application.Windows;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Application.Rules;

/// <summary>
/// Reactive rule enforcement for the active room (§7.5). Subscribes to window/process
/// creation and, for the active room only, applies the room's <see cref="BlockReaction"/> to
/// disallowed windows. Also adopts unassigned windows under the current-room policy. Skips
/// everything while a switch batch is in progress, to avoid reacting to our own changes.
/// </summary>
public interface IRuleEnforcer
{
    /// <summary>Begin listening to OS window/process events. Call once at startup.</summary>
    void Start();

    /// <summary>Reconfigure for a new active room (or null when none is active).</summary>
    void SetActiveRoom(Room? room);
}

/// <inheritdoc cref="IRuleEnforcer" />
public sealed class RuleEnforcer : IRuleEnforcer, IDisposable
{
    private readonly IWindowService _windows;
    private readonly IProcessService _processes;
    private readonly IRuleEngine _rules;
    private readonly IWindowRegistry _registry;
    private readonly ISwitchGate _gate;
    private readonly INotificationService _notifications;
    private readonly ILogger<RuleEnforcer> _logger;

    private Room? _active;
    private bool _started;

    public RuleEnforcer(
        IWindowService windows,
        IProcessService processes,
        IRuleEngine rules,
        IWindowRegistry registry,
        ISwitchGate gate,
        INotificationService notifications,
        ILogger<RuleEnforcer> logger)
    {
        _windows = windows;
        _processes = processes;
        _rules = rules;
        _registry = registry;
        _gate = gate;
        _notifications = notifications;
        _logger = logger;
    }

    public void Start()
    {
        if (_started)
            return;

        _started = true;
        _windows.WindowCreated += OnWindowCreated;
        _windows.WindowActivated += OnWindowActivated;
        _processes.ProcessStarted += OnProcessStarted;
    }

    public void SetActiveRoom(Room? room) => _active = room;

    private void OnWindowCreated(object? sender, WindowInfo window) => HandleAppearance(window, reShow: false);

    private void OnWindowActivated(object? sender, WindowInfo window) => HandleAppearance(window, reShow: true);

    private void HandleAppearance(WindowInfo window, bool reShow)
    {
        if (_gate.IsBatching) // don't react to our own switch
            return;

        var room = _active;
        if (room is null || room.IsCatchAll) // catch-all shows loose windows; don't claim/move them
            return;

        var owner = _registry.GetExplicitRoom(window);

        // Already part of this room (explicitly, or via the room's match rules): leave it be.
        if (owner == room.Id || (owner is null && _rules.IsOwnedBy(window, room)))
            return;

        // reShow == true means the user actively brought this window to the foreground here
        // (relaunched / Alt-Tabbed a single-instance app, or launched something new). THAT is the
        // signal to make it part of this room - not a background show event (a song change, a
        // notification) which would otherwise drag every loose app into whatever room you're in.
        if (reShow)
        {
            if (owner is not null)
                _registry.AssignManual(window.Handle, room.Id); // follow it in from another room
            else if (_registry.UnassignedPolicy == UnassignedWindowPolicy.CurrentRoom)
                _registry.Adopt(window.Handle, room.Id);        // a loose window you opened here
            _windows.Show(window.Handle);                       // re-attach its taskbar button
            _logger.LogDebug("{Process} joined room {Room} (brought to foreground).", window.ProcessName, room.Name);
            return;
        }

        // Background appearance of something that doesn't belong here. Honour an explicit block
        // reaction if the room defines one...
        var reaction = _rules.GetBlockReaction(window, room);
        if (reaction is not null)
        {
            ApplyBlockReaction(window, room, reaction.Value);
            return;
        }

        // ...otherwise, if it's a window owned by another room or one we already hid for this room
        // (a loose app like Spotify/Discord ticking in the background) that re-revealed itself,
        // hide it again so this room's taskbar stays clean. A brand-new loose window we haven't
        // classified is left alone - it'll join if you foreground it, or hide on the next switch.
        if (owner is not null || _windows.HiddenWindows.Contains(window.Handle))
        {
            _windows.Hide(window.Handle);
            _logger.LogDebug("Re-hid {Process}; it surfaced in room {Room} but isn't part of it.", window.ProcessName, room.Name);
        }
    }

    private void OnProcessStarted(object? sender, ProcessInfo process)
    {
        // ProcessStarted is an optional watcher that may never fire; blocking is handled as the
        // window appears (OnWindowCreated). TODO: pre-emptive process blocking before any window.
    }

    private void ApplyBlockReaction(WindowInfo window, Room room, BlockReaction reaction)
    {
        switch (reaction)
        {
            case BlockReaction.CloseProcess:
                _notifications.ShowToast("Closing blocked app", $"{window.ProcessName} isn't allowed in \"{room.Name}\".");
                _processes.TryCloseGracefully(window.ProcessId); // WM_CLOSE first, so unsaved work can be saved
                break;
            case BlockReaction.MinimizeWindow:
                _windows.Minimize(window.Handle);
                break;
            case BlockReaction.WarnOnly:
                _notifications.ShowToast("Blocked in this room", $"{window.ProcessName} isn't part of \"{room.Name}\".");
                break;
        }

        _logger.LogInformation(
            "Applied {Reaction} to {Process} (room {Room}).", reaction, window.ProcessName, room.Name);
    }

    public void Dispose()
    {
        if (!_started)
            return;

        _windows.WindowCreated -= OnWindowCreated;
        _windows.WindowActivated -= OnWindowActivated;
        _processes.ProcessStarted -= OnProcessStarted;
    }
}
