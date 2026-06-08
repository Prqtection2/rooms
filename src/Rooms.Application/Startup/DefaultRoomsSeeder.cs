using Microsoft.Extensions.Logging;
using Rooms.Application.Hotkeys;
using Rooms.Application.Rooms;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Application.Startup;

/// <summary>
/// Seeds the friendly starter set of rooms (on first run, and from the Settings "Set up testing
/// rooms" button): a catch-all <b>Home</b> that always keeps whatever you already had open, plus a
/// clean <b>Notepad</b> room and a clean <b>Microsoft Edge</b> room.
///
/// Home is a catch-all, so every "loose" window (anything not claimed by a focused room) lives
/// there and is always recoverable. The unassigned-window policy is set to <c>CurrentRoom</c> so
/// the Notepad/Edge rooms stay clean - your other apps are hidden while you're in them and
/// reappear the instant you switch back to Home. That means a user (or a tester) can switch
/// Home → Notepad → Home without ever losing the windows they started with.
/// </summary>
public interface IDefaultRoomsSeeder
{
    /// <summary>Seed the starter rooms only if none exist yet. Returns true if it seeded.</summary>
    Task<bool> SeedIfEmptyAsync(CancellationToken ct = default);

    /// <summary>(Re)create the starter rooms plus their hotkeys, default room, and window policy.
    /// Idempotent: an existing room with the same name is reused, never duplicated.</summary>
    Task SeedAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IDefaultRoomsSeeder" />
public sealed class DefaultRoomsSeeder : IDefaultRoomsSeeder
{
    // Ctrl+Alt as the shared modifier for the starter bindings.
    private const ModifierKeys Mods = ModifierKeys.Control | ModifierKeys.Alt;
    private const uint Vk1 = 0x31, Vk2 = 0x32, Vk3 = 0x33, VkR = 0x52, VkF = 0x46;

    private readonly IRoomManager _rooms;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IHotkeyService _hotkeys;
    private readonly ILogger<DefaultRoomsSeeder> _logger;

    public DefaultRoomsSeeder(
        IRoomManager rooms,
        IAppSettingsStore settingsStore,
        IHotkeyService hotkeys,
        ILogger<DefaultRoomsSeeder> logger)
    {
        _rooms = rooms;
        _settingsStore = settingsStore;
        _hotkeys = hotkeys;
        _logger = logger;
    }

    public async Task<bool> SeedIfEmptyAsync(CancellationToken ct = default)
    {
        if (_rooms.Rooms.Count > 0)
            return false;

        await SeedAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Home keeps every loose window visible, so the stuff you had open is never lost.
        var home = await EnsureRoomAsync("Home", catchAll: true, "#6B7280", matcher: null, autoLaunch: null, ct)
            .ConfigureAwait(false);

        // Notepad / Microsoft Edge are focused rooms that own (and auto-launch) just their one app.
        var notepad = await EnsureRoomAsync("Notepad", catchAll: false, "#3B82F6", "notepad", "notepad.exe", ct)
            .ConfigureAwait(false);
        var edge = await EnsureRoomAsync("Microsoft Edge", catchAll: false, "#10B981", "msedge", "msedge", ct)
            .ConfigureAwait(false);

        var settings = await _settingsStore.LoadAsync(ct).ConfigureAwait(false);

        // Loose windows belong to Home only - so Notepad/Edge stay clean and your other apps come
        // straight back when you return to Home.
        settings.UnassignedPolicy = UnassignedWindowPolicy.CurrentRoom;

        // Land in Home on launch, with everything you had open visible.
        settings.DefaultRoomId = home.Id;

        settings.Hotkeys = new List<HotkeyBinding>
        {
            new(HotkeyActions.SwitchToRoom, new HotkeyDefinition(Mods, Vk1), home.Id),    // Ctrl+Alt+1
            new(HotkeyActions.SwitchToRoom, new HotkeyDefinition(Mods, Vk2), notepad.Id), // Ctrl+Alt+2
            new(HotkeyActions.SwitchToRoom, new HotkeyDefinition(Mods, Vk3), edge.Id),    // Ctrl+Alt+3
            new(HotkeyActions.OpenSwitcher, new HotkeyDefinition(Mods, VkR), null),       // Ctrl+Alt+R
            new(HotkeyActions.ToggleFocus, new HotkeyDefinition(Mods, VkF), null),        // Ctrl+Alt+F
        };

        await _settingsStore.SaveAsync(settings, ct).ConfigureAwait(false);
        await _hotkeys.ReloadAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Seeded starter rooms: Home (catch-all), Notepad, Microsoft Edge.");
    }

    private async Task<Room> EnsureRoomAsync(
        string name, bool catchAll, string accent, string? matcher, string? autoLaunch, CancellationToken ct)
    {
        var existing = _rooms.Rooms.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = name,
            OrderIndex = _rooms.Rooms.Count,
            IsCatchAll = catchAll,
            AccentColorHex = accent,
        };
        if (matcher is not null)
            room.OwnedWindowMatchers.Add(new WindowMatcher { ProcessName = matcher });
        if (autoLaunch is not null)
            room.AutoLaunchApps.Add(new AppLaunchSpec { ExecutablePath = autoLaunch, LaunchOnEnter = true });

        await _rooms.UpdateAsync(room, ct).ConfigureAwait(false);
        return room;
    }
}
