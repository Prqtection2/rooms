using Microsoft.Extensions.Logging;
using Rooms.Application.Rooms;
using Rooms.Application.Rules;
using Rooms.Application.Switching;
using Rooms.Application.Windows;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Application.Focus;

/// <summary>
/// Optional, opt-in focus re-assertion (§10): while a locked focus session runs, hide windows
/// that intrude on the focus room as they appear. This is friction-based and explicitly
/// bypassable - it only reacts to newly created windows, never fights Task Manager, and is not a
/// kernel driver. Off by default (<see cref="AppSettings.FocusReassertion"/>).
/// </summary>
public interface IFocusReassertionService
{
    void Start();
}

/// <inheritdoc cref="IFocusReassertionService" />
public sealed class FocusReassertionService : IFocusReassertionService, IDisposable
{
    private readonly IFocusSessionService _focus;
    private readonly IRoomManager _rooms;
    private readonly IWindowService _windows;
    private readonly IRuleEngine _rules;
    private readonly IWindowRegistry _registry;
    private readonly ISwitchGate _gate;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILogger<FocusReassertionService> _logger;

    private bool _enabled;
    private bool _started;

    public FocusReassertionService(
        IFocusSessionService focus,
        IRoomManager rooms,
        IWindowService windows,
        IRuleEngine rules,
        IWindowRegistry registry,
        ISwitchGate gate,
        IAppSettingsStore settingsStore,
        ILogger<FocusReassertionService> logger)
    {
        _focus = focus;
        _rooms = rooms;
        _windows = windows;
        _rules = rules;
        _registry = registry;
        _gate = gate;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public void Start()
    {
        if (_started)
            return;

        _started = true;
        _focus.SessionStarted += (_, _) => _ = RefreshEnabledAsync();
        _windows.WindowCreated += OnWindowCreated;
    }

    private async Task RefreshEnabledAsync()
    {
        var settings = await _settingsStore.LoadAsync().ConfigureAwait(false);
        _enabled = settings.FocusReassertion;
    }

    private void OnWindowCreated(object? sender, WindowInfo window)
    {
        if (!_enabled || _gate.IsBatching)
            return;

        var session = _focus.Current;
        if (session is null || session.AllowEarlyExit) // only locked sessions re-assert
            return;

        var room = _rooms.Get(session.RoomId);
        if (room is null)
            return;

        // Leave the shell/system UI and windows that belong to the focus room alone.
        if (SafeProcessList.IsNeverHidden(window.ProcessName) ||
            _registry.GetExplicitRoom(window) == room.Id ||
            _rules.IsOwnedBy(window, room))
        {
            return;
        }

        _windows.Hide(window.Handle);
        _logger.LogInformation(
            "Focus re-assertion hid intruding window {Process} during focus on \"{Room}\".",
            window.ProcessName, room.Name);
    }

    public void Dispose()
    {
        if (_started)
            _windows.WindowCreated -= OnWindowCreated;
    }
}
