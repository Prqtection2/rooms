using Microsoft.Extensions.Logging;
using Rooms.Application.Focus;
using Rooms.Application.Rooms;
using Rooms.Application.Rules;
using Rooms.Application.Windows;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Application.Switching;

/// <summary>
/// Implements the §6.2 switch algorithm with the §10 focus-friction intercept. Computes the
/// desired window set from the window registry + rule engine, hides/shows the difference inside
/// a batch (so the RuleEngine doesn't react to our own changes), launches missing apps, applies
/// rules + ambient + website rules, brings the room's primary window forward, and persists state.
/// </summary>
public sealed class SwitchOrchestrator : ISwitchOrchestrator
{
    private readonly IRoomManager _rooms;
    private readonly IWindowService _windows;
    private readonly IProcessService _processes;
    private readonly IRuleEngine _rules;
    private readonly IWindowRegistry _registry;
    private readonly ISwitchGate _gate;
    private readonly IRuleEnforcer _ruleEnforcer;
    private readonly IFocusSessionService _focus;
    private readonly IWallpaperService _wallpaper;
    private readonly INotificationService _notifications;
    private readonly IWebsiteBlocker _websiteBlocker;
    private readonly IAppStateStore _stateStore;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILogger<SwitchOrchestrator> _logger;

    public SwitchOrchestrator(
        IRoomManager rooms,
        IWindowService windows,
        IProcessService processes,
        IRuleEngine rules,
        IWindowRegistry registry,
        ISwitchGate gate,
        IRuleEnforcer ruleEnforcer,
        IFocusSessionService focus,
        IWallpaperService wallpaper,
        INotificationService notifications,
        IWebsiteBlocker websiteBlocker,
        IAppStateStore stateStore,
        IAppSettingsStore settingsStore,
        ILogger<SwitchOrchestrator> logger)
    {
        _rooms = rooms;
        _windows = windows;
        _processes = processes;
        _rules = rules;
        _registry = registry;
        _gate = gate;
        _ruleEnforcer = ruleEnforcer;
        _focus = focus;
        _wallpaper = wallpaper;
        _notifications = notifications;
        _websiteBlocker = websiteBlocker;
        _stateStore = stateStore;
        _settingsStore = settingsStore;
        _logger = logger;

        _rooms.ActiveRoomChanged += (_, e) => ActiveRoomChanged?.Invoke(this, e);
    }

    public event EventHandler<RoomActivatedEventArgs>? ActiveRoomChanged;

    public async Task<SwitchResult> SwitchToAsync(Guid roomId, bool endFocusSession = false, CancellationToken ct = default)
    {
        var room = _rooms.Get(roomId);
        if (room is null)
        {
            _logger.LogWarning("Switch requested for unknown room {RoomId}.", roomId);
            return SwitchResult.Failed(roomId, "Room not found.");
        }

        // §10 friction: a locked focus session blocks switching away unless the user gives up.
        var session = _focus.Current;
        if (session is not null && !session.AllowEarlyExit && session.RoomId != roomId)
        {
            if (!endFocusSession)
            {
                _logger.LogInformation("Switch to {Name} blocked by an active focus session.", room.Name);
                return SwitchResult.BlockedByFocusSession(roomId, session.EndsAt - DateTimeOffset.UtcNow);
            }

            _focus.Stop(); // the user chose to give up
        }

        _logger.LogInformation("Switching to room {RoomId} ({Name}).", room.Id, room.Name);

        var settings = await _settingsStore.LoadAsync(ct).ConfigureAwait(false);
        _registry.UnassignedPolicy = settings.UnassignedPolicy;
        var globalProcesses = BuildGlobalProcessSet(settings);

        // 1-3. snapshot vs desired -> toShow / toHide.
        var windows = _windows.EnumerateTopLevelWindows();
        var ourHidden = new HashSet<IntPtr>(_windows.HiddenWindows);
        var toShow = new List<WindowInfo>();
        var toHide = new List<WindowInfo>();
        WindowInfo? primary = null;

        foreach (var window in windows)
        {
            ct.ThrowIfCancellationRequested();

            // A "home"/catch-all room shows your loose windows - everything NOT claimed by a
            // focused room - so a window you opened in another room stays there, not here.
            var desired = room.IsCatchAll
                ? IsHomeDesired(window, room)
                : IsDesired(window, room, globalProcesses);

            if (desired)
            {
                if (!room.IsCatchAll && primary is null && OwnedByTarget(window, room))
                    primary = window;

                // Only restore windows WE hid (those are SW_HIDE-hidden: no taskbar presence) -
                // leave windows the user minimised themselves alone.
                if (!window.HasTaskbarPresence && ourHidden.Contains(window.Handle))
                    toShow.Add(window);
            }
            else if (window.HasTaskbarPresence)
            {
                // Hide anything with a taskbar button that doesn't belong here - INCLUDING minimised
                // windows (Discord/Spotify left minimised), which still own a taskbar button.
                toHide.Add(window);
            }
        }

        _logger.LogInformation(
            "Switch to {Room}: hiding [{Hide}]; showing [{Show}].",
            room.Name,
            string.Join(", ", toHide.Select(w => $"{w.ProcessName}#{w.Handle}")),
            string.Join(", ", toShow.Select(w => $"{w.ProcessName}#{w.Handle}")));

        int launched;

        // 4. Begin batch (suspend our own WinEvent reactions).
        using (_gate.Begin())
        {
            foreach (var window in toHide) // 5. hide
                _windows.Hide(window.Handle);

            launched = LaunchAutoApps(room, windows); // 6. launch (returns immediately)

            foreach (var window in toShow) // 7. show
                _windows.Show(window.Handle);

            _ruleEnforcer.SetActiveRoom(room); // 8. rules
            ApplyAmbient(room);                 // 9. ambient
            if (primary is not null)            // 10. foreground
                _windows.BringToForeground(primary.Handle);
        }
        // 11. End batch.

        // 12. Persist active room + state (last active + failsafe hidden set).
        _rooms.SetActiveRoom(room);
        await PersistStateAsync(room.Id, ct).ConfigureAwait(false);

        await ApplyWebsiteRulesAsync(room).ConfigureAwait(false); // §9

        var result = new SwitchResult(true, room.Id, toShow.Count, toHide.Count, launched);
        _logger.LogInformation(
            "Switched to {Name}: {Shown} shown, {Hidden} hidden, {Launched} launched.",
            room.Name, result.WindowsShown, result.WindowsHidden, result.AppsLaunched);
        return result;
    }

    public Task<SwitchResult> ResetRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        // Forget windows the user added this session so only the room's defaults remain,
        // then re-enter (which relaunches auto-launch apps and hides everything else).
        _registry.ClearRuntimeAssignments(roomId);
        _logger.LogInformation("Reset room {RoomId} to its defaults.", roomId);
        return SwitchToAsync(roomId, ct: ct);
    }

    public async Task ShowAllAsync(CancellationToken ct = default)
    {
        // Clear the active room first so the windows we re-show aren't "followed" into the
        // previously-active room by the rule enforcer.
        _rooms.SetActiveRoom(null);
        _ruleEnforcer.SetActiveRoom(null);
        _windows.RestoreAllHidden();

        await _stateStore.SaveAsync(new AppState
        {
            LastActiveRoomId = null,
            HiddenWindowHandles = new List<long>(),
        }, ct).ConfigureAwait(false);

        _logger.LogInformation("Showed all windows and left all rooms.");
    }

    private HashSet<string> BuildGlobalProcessSet(AppSettings settings)
    {
        var set = new HashSet<string>(SafeProcessList.Names, StringComparer.OrdinalIgnoreCase);
        set.UnionWith(settings.GlobalStickyProcessNames);
        return set;
    }

    private bool IsDesired(WindowInfo window, Room target, HashSet<string> globalProcesses) =>
        OwnedByTarget(window, target) || IsGlobal(window, globalProcesses);

    /// <summary>A home/catch-all room shows your "loose" windows: those not claimed by a focused
    /// room - either explicitly (launched / adopted / manual) or by that room's match rules.</summary>
    private bool IsHomeDesired(WindowInfo window, Room room)
    {
        var explicitRoom = _registry.GetExplicitRoom(window);
        if (explicitRoom is not null)
            return explicitRoom == room.Id;

        // Not explicitly owned: it's loose unless a focused room's rules claim it.
        return !_rooms.Rooms.Any(r => !r.IsCatchAll && r.Id != room.Id && _rules.IsOwnedBy(window, r));
    }

    private bool OwnedByTarget(WindowInfo window, Room target)
    {
        var explicitRoom = _registry.GetExplicitRoom(window);
        if (explicitRoom == target.Id)
            return true;
        if (explicitRoom is not null)
            return false;

        return _rules.IsOwnedBy(window, target);
    }

    private bool IsGlobal(WindowInfo window, HashSet<string> globalProcesses)
    {
        // Shell/system UI + user-configured global processes are never hidden (§8).
        if (window.ProcessName.Length > 0 && globalProcesses.Contains(window.ProcessName))
            return true;

        if (_registry.IsSticky(window.Handle))
            return true;

        if (_registry.UnassignedPolicy != UnassignedWindowPolicy.Sticky)
            return false;

        if (_registry.GetExplicitRoom(window) is not null)
            return false;

        // Unassigned (matches no room's rules) + sticky policy -> visible everywhere.
        return !_rooms.Rooms.Any(r => _rules.IsOwnedBy(window, r));
    }

    private int LaunchAutoApps(Room room, IReadOnlyList<WindowInfo> openWindows)
    {
        var launched = 0;

        foreach (var spec in room.AutoLaunchApps)
        {
            if (!spec.LaunchOnEnter || string.IsNullOrWhiteSpace(spec.ExecutablePath))
                continue;

            // "Already open?" - for an isolated app, only THIS room's own instance counts (so a
            // second room launches its own); otherwise any matching window counts (don't duplicate
            // single-instance apps). Compare by process name, since the spec path ("notepad.exe")
            // rarely equals the window's full path.
            var alreadyOpen = spec.IsolatedInstance
                ? openWindows.Any(w => MatchesLaunchedApp(w, spec) && OwnedByTarget(w, room))
                : openWindows.Any(w => MatchesLaunchedApp(w, spec));
            if (alreadyOpen)
                continue;

            var pid = spec.IsolatedInstance
                ? _processes.LaunchIsolated(spec, room.Id.ToString("n"))
                : _processes.Launch(spec);

            if (pid != 0)
            {
                _registry.TrackLaunch(pid, room.Id);
                launched++;
                // TODO (§6): apply spec.DesiredPlacement once the launched window appears.
            }
            else
            {
                _logger.LogWarning("Failed to launch {Path}.", spec.ExecutablePath);
            }
        }

        return launched;
    }

    private static bool MatchesLaunchedApp(WindowInfo window, AppLaunchSpec spec)
    {
        if (!string.IsNullOrWhiteSpace(window.ExecutablePath) &&
            string.Equals(window.ExecutablePath, spec.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            return true;

        var specName = System.IO.Path.GetFileNameWithoutExtension(spec.ExecutablePath);
        return !string.IsNullOrEmpty(specName) &&
            string.Equals(window.ProcessName, specName, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyAmbient(Room room)
    {
        if (!string.IsNullOrWhiteSpace(room.Ambient.WallpaperPath))
            _wallpaper.SetWallpaper(room.Ambient.WallpaperPath!);

        _notifications.SetDoNotDisturb(room.Ambient.EnableDoNotDisturb);
    }

    private async Task PersistStateAsync(Guid activeRoomId, CancellationToken ct)
    {
        var state = new AppState
        {
            LastActiveRoomId = activeRoomId,
            HiddenWindowHandles = _windows.HiddenWindows.Select(h => h.ToInt64()).ToList(),
        };

        await _stateStore.SaveAsync(state, ct).ConfigureAwait(false);
    }

    private async Task ApplyWebsiteRulesAsync(Room room)
    {
        try
        {
            if (room.Rules.BlockedDomains.Count > 0)
                await _websiteBlocker.ApplyAsync(room.Rules.BlockedDomains).ConfigureAwait(false);
            else
                await _websiteBlocker.ClearAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Applying website rules for room {Room} failed.", room.Name);
        }
    }
}
