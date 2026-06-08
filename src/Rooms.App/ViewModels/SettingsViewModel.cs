using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rooms.Application.Hotkeys;
using Rooms.Application.Rooms;
using Rooms.Application.Startup;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.App.ViewModels;

/// <summary>Choice of room for the "default room" dropdown (null = none).</summary>
public sealed record RoomChoice(Guid? Id, string Name);

/// <summary>
/// Settings (§12.4): unassigned-window policy, run-at-startup, focus re-assertion, default room,
/// website-blocker status, and live hotkey capture with conflict detection. While the window is
/// open the live hotkeys are suspended so combos can be probed for availability.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IRoomManager _rooms;
    private readonly IHotkeyService _hotkeys;
    private readonly IHotkeyRegistrar _registrar;
    private readonly IDefaultRoomsSeeder _seeder;

    private AppSettings _settings = new();

    public SettingsViewModel(
        IAppSettingsStore settingsStore,
        IRoomManager rooms,
        IHotkeyService hotkeys,
        IHotkeyRegistrar registrar,
        IDefaultRoomsSeeder seeder)
    {
        _settingsStore = settingsStore;
        _rooms = rooms;
        _hotkeys = hotkeys;
        _registrar = registrar;
        _seeder = seeder;
    }

    [ObservableProperty]
    private bool _runAtStartup;

    [ObservableProperty]
    private bool _stickyUnassignedWindows = true;

    [ObservableProperty]
    private bool _focusReassertion;

    [ObservableProperty]
    private int _defaultFocusMinutes = 25;

    [ObservableProperty]
    private bool _useHostsFileBlocking;

    [ObservableProperty]
    private string _websiteBlockerStatus = string.Empty;

    [ObservableProperty]
    private RoomChoice? _defaultRoom;

    [ObservableProperty]
    private HotkeyRowViewModel? _capturingRow;

    public ObservableCollection<RoomChoice> RoomChoices { get; } = new();

    public ObservableCollection<HotkeyRowViewModel> HotkeyRows { get; } = new();

    public event EventHandler? Saved;

    public async Task InitializeAsync()
    {
        _settings = await _settingsStore.LoadAsync();

        RunAtStartup = _settings.RunAtStartup;
        StickyUnassignedWindows = _settings.UnassignedPolicy == UnassignedWindowPolicy.Sticky;
        FocusReassertion = _settings.FocusReassertion;
        DefaultFocusMinutes = _settings.DefaultFocusMinutes > 0 ? _settings.DefaultFocusMinutes : 25;
        UseHostsFileBlocking = _settings.WebsiteBlocking == WebsiteBlockingMode.HostsFile;
        UpdateWebsiteStatus();

        RoomChoices.Clear();
        RoomChoices.Add(new RoomChoice(null, "(none)"));
        foreach (var room in _rooms.Rooms.OrderBy(r => r.OrderIndex))
            RoomChoices.Add(new RoomChoice(room.Id, room.Name));
        DefaultRoom = RoomChoices.FirstOrDefault(c => c.Id == _settings.DefaultRoomId) ?? RoomChoices[0];

        BuildHotkeyRows();

        // Release live hotkeys so we can probe combos for availability while editing.
        _hotkeys.Suspend();
    }

    private void BuildHotkeyRows()
    {
        HotkeyRows.Clear();
        HotkeyRows.Add(Row("Open switcher", HotkeyActions.OpenSwitcher, null));
        HotkeyRows.Add(Row("Start / stop focus", HotkeyActions.ToggleFocus, null));
        HotkeyRows.Add(Row("Next room", HotkeyActions.NextRoom, null));
        HotkeyRows.Add(Row("Previous room", HotkeyActions.PreviousRoom, null));

        foreach (var room in _rooms.Rooms.OrderBy(r => r.OrderIndex))
            HotkeyRows.Add(Row($"Switch to \"{room.Name}\"", HotkeyActions.SwitchToRoom, room.Id));

        HotkeyRowViewModel Row(string label, string actionKey, Guid? roomId)
        {
            var hotkey = _settings.Hotkeys
                .FirstOrDefault(b => b.ActionKey == actionKey && b.RoomId == roomId)?.Hotkey;
            return new HotkeyRowViewModel(label, actionKey, roomId, hotkey);
        }
    }

    [RelayCommand]
    private void StartCapture(HotkeyRowViewModel? row)
    {
        if (row is null)
            return;

        foreach (var other in HotkeyRows)
            other.IsCapturing = false;

        row.IsCapturing = true;
        CapturingRow = row;
    }

    [RelayCommand]
    private void ClearHotkey(HotkeyRowViewModel? row)
    {
        if (row is null)
            return;

        row.VirtualKey = 0;
        row.Modifiers = ModifierKeys.None;
        row.Conflict = null;
        row.IsCapturing = false;
        if (ReferenceEquals(CapturingRow, row))
            CapturingRow = null;
    }

    /// <summary>Called by the window when a key combo is pressed during capture.</summary>
    public void ApplyCapture(ModifierKeys modifiers, uint virtualKey)
    {
        if (CapturingRow is not { } row)
            return;

        row.Modifiers = modifiers;
        row.VirtualKey = virtualKey;
        row.IsCapturing = false;
        CapturingRow = null;
        CheckConflicts(row);
    }

    public void CancelCapture()
    {
        if (CapturingRow is { } row)
            row.IsCapturing = false;
        CapturingRow = null;
    }

    private void CheckConflicts(HotkeyRowViewModel row)
    {
        if (!row.HasBinding)
        {
            row.Conflict = null;
            return;
        }

        var duplicate = HotkeyRows.FirstOrDefault(r =>
            !ReferenceEquals(r, row) && r.HasBinding && r.Modifiers == row.Modifiers && r.VirtualKey == row.VirtualKey);
        if (duplicate is not null)
        {
            row.Conflict = $"Also bound to \"{duplicate.ActionLabel}\".";
            return;
        }

        // Our own hotkeys are suspended, so a failed registration means another app owns it.
        var id = _registrar.Register(row.ToDefinition()!);
        if (id < 0)
        {
            row.Conflict = "In use by another app.";
        }
        else
        {
            _registrar.Unregister(id);
            row.Conflict = null;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _settings.RunAtStartup = RunAtStartup;
        _settings.UnassignedPolicy = StickyUnassignedWindows
            ? UnassignedWindowPolicy.Sticky
            : UnassignedWindowPolicy.CurrentRoom;
        _settings.FocusReassertion = FocusReassertion;
        _settings.DefaultFocusMinutes = DefaultFocusMinutes > 0 ? DefaultFocusMinutes : 25;
        _settings.WebsiteBlocking = UseHostsFileBlocking
            ? WebsiteBlockingMode.HostsFile
            : WebsiteBlockingMode.BrowserGating;
        _settings.DefaultRoomId = DefaultRoom?.Id;
        _settings.Hotkeys = HotkeyRows
            .Where(r => r.HasBinding)
            .Select(r => new HotkeyBinding(r.ActionKey, r.ToDefinition()!, r.RoomId))
            .ToList();

        await _settingsStore.SaveAsync(_settings);
        await _hotkeys.ReloadAsync();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>One-click setup of the starter rooms: a catch-all "Home" that keeps every window
    /// you have open, plus focused Notepad and Edge rooms, bound to Ctrl+Alt+1/2/3 (and
    /// Ctrl+Alt+R / Ctrl+Alt+F). Same set that's seeded on first run.</summary>
    [RelayCommand]
    private async Task SetUpTestingRoomsAsync()
    {
        await _seeder.SeedAsync();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    partial void OnUseHostsFileBlockingChanged(bool value) => UpdateWebsiteStatus();

    private void UpdateWebsiteStatus() => WebsiteBlockerStatus = UseHostsFileBlocking
        ? "Option B (hosts file): blocks domains system-wide. Requires running Rooms as administrator; otherwise it no-ops."
        : "Option A (browser gating): blocks sites by closing/minimising the browser via the room's rules. No admin needed.";

    /// <summary>Re-arm the live hotkeys (called when the window closes, saved or not).</summary>
    public Task RestoreHotkeysAsync() => _hotkeys.ReloadAsync();
}
