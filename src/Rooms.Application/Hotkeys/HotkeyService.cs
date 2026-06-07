using Microsoft.Extensions.Logging;
using Rooms.Application.Rooms;
using Rooms.Application.Switching;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Application.Hotkeys;

/// <inheritdoc cref="IHotkeyService" />
public sealed class HotkeyService : IHotkeyService, IDisposable
{
    private readonly IHotkeyRegistrar _registrar;
    private readonly IRoomManager _rooms;
    private readonly ISwitchOrchestrator _orchestrator;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILogger<HotkeyService> _logger;

    private readonly Dictionary<int, HotkeyBinding> _bindings = new();

    public HotkeyService(
        IHotkeyRegistrar registrar,
        IRoomManager rooms,
        ISwitchOrchestrator orchestrator,
        IAppSettingsStore settingsStore,
        ILogger<HotkeyService> logger)
    {
        _registrar = registrar;
        _rooms = rooms;
        _orchestrator = orchestrator;
        _settingsStore = settingsStore;
        _logger = logger;

        _registrar.HotkeyPressed += OnHotkeyPressed;
    }

    public event EventHandler? SwitcherHotkeyPressed;

    public event EventHandler? ToggleFocusPressed;

    public Task InitializeAsync(CancellationToken ct = default) => ReloadAsync(ct);

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        UnregisterAll();

        var settings = await _settingsStore.LoadAsync(ct).ConfigureAwait(false);
        foreach (var binding in settings.Hotkeys)
        {
            var id = _registrar.Register(binding.Hotkey);
            if (id >= 0)
                _bindings[id] = binding;
            else
                _logger.LogWarning("Failed to register hotkey {Hotkey} for action {Action}.", binding.Hotkey, binding.ActionKey);
        }
    }

    private void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (!_bindings.TryGetValue(hotkeyId, out var binding))
            return;

        switch (binding.ActionKey)
        {
            case HotkeyActions.OpenSwitcher:
                SwitcherHotkeyPressed?.Invoke(this, EventArgs.Empty);
                break;
            case HotkeyActions.ToggleFocus:
                ToggleFocusPressed?.Invoke(this, EventArgs.Empty);
                break;
            case HotkeyActions.NextRoom:
                _ = SwitchRelativeAsync(+1);
                break;
            case HotkeyActions.PreviousRoom:
                _ = SwitchRelativeAsync(-1);
                break;
            case HotkeyActions.SwitchToRoom when binding.RoomId is Guid roomId:
                _ = SwitchAsync(roomId);
                break;
        }
    }

    private Task SwitchRelativeAsync(int direction)
    {
        var ordered = _rooms.Rooms.OrderBy(r => r.OrderIndex).ThenBy(r => r.Name).ToList();
        if (ordered.Count == 0)
            return Task.CompletedTask;

        var active = _rooms.ActiveRoom;
        var currentIndex = active is null ? -1 : ordered.FindIndex(r => r.Id == active.Id);

        var targetIndex = currentIndex < 0
            ? (direction > 0 ? 0 : ordered.Count - 1)
            : ((currentIndex + direction) % ordered.Count + ordered.Count) % ordered.Count;

        return SwitchAsync(ordered[targetIndex].Id);
    }

    private async Task SwitchAsync(Guid roomId)
    {
        try
        {
            await _orchestrator.SwitchToAsync(roomId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotkey switch to room {RoomId} failed.", roomId);
        }
    }

    public void Suspend() => UnregisterAll();

    private void UnregisterAll()
    {
        foreach (var id in _bindings.Keys)
            _registrar.Unregister(id);

        _bindings.Clear();
    }

    public void Dispose()
    {
        _registrar.HotkeyPressed -= OnHotkeyPressed;
        UnregisterAll();
    }
}
