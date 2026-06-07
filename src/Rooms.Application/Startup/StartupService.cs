using Microsoft.Extensions.Logging;
using Rooms.Application.Rooms;
using Rooms.Application.Switching;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Application.Startup;

/// <summary>
/// Application-layer startup orchestration (§7.6): runs the lost-window failsafe from persisted
/// state, syncs run-at-login, and restores the last active room (falling back to the default).
/// </summary>
public interface IStartupService
{
    Task RunStartupAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IStartupService" />
public sealed class StartupService : IStartupService
{
    private readonly IWindowService _windows;
    private readonly IAppStateStore _stateStore;
    private readonly IAutoStartService _autoStart;
    private readonly ISwitchOrchestrator _orchestrator;
    private readonly IRoomManager _rooms;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILogger<StartupService> _logger;

    public StartupService(
        IWindowService windows,
        IAppStateStore stateStore,
        IAutoStartService autoStart,
        ISwitchOrchestrator orchestrator,
        IRoomManager rooms,
        IAppSettingsStore settingsStore,
        ILogger<StartupService> logger)
    {
        _windows = windows;
        _stateStore = stateStore;
        _autoStart = autoStart;
        _orchestrator = orchestrator;
        _rooms = rooms;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public async Task RunStartupAsync(CancellationToken ct = default)
    {
        var state = await _stateStore.LoadAsync(ct).ConfigureAwait(false);
        await RecoverStrandedWindowsAsync(state, ct).ConfigureAwait(false);

        var settings = await _settingsStore.LoadAsync(ct).ConfigureAwait(false);
        SyncAutoStart(settings.RunAtStartup);

        // Restore the last active room, falling back to the configured default.
        var targetId = state.LastActiveRoomId ?? settings.DefaultRoomId;
        if (targetId is Guid id && _rooms.Get(id) is not null)
            await _orchestrator.SwitchToAsync(id, ct: ct).ConfigureAwait(false);
    }

    /// <summary>The §6 lost-window failsafe: re-show anything a previous (possibly crashed) run
    /// left hidden, then clear that part of the state. Stale handles no-op (IsWindow-guarded).</summary>
    private async Task RecoverStrandedWindowsAsync(AppState state, CancellationToken ct)
    {
        if (state.HiddenWindowHandles.Count == 0)
            return;

        _logger.LogWarning(
            "Recovering {Count} possibly-stranded hidden window(s) from a previous run.",
            state.HiddenWindowHandles.Count);

        foreach (var handle in state.HiddenWindowHandles)
            _windows.Show((IntPtr)handle);

        state.HiddenWindowHandles.Clear();
        await _stateStore.SaveAsync(state, ct).ConfigureAwait(false);
    }

    private void SyncAutoStart(bool desired)
    {
        var current = _autoStart.IsEnabled();
        if (desired && !current)
            _autoStart.Enable();
        else if (!desired && current)
            _autoStart.Disable();
    }
}
