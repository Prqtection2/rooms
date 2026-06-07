using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rooms.Application.Rooms;
using Rooms.Application.Switching;

namespace Rooms.App.ViewModels;

/// <summary>
/// Backs both the tray popup (§12.1) and the switcher overlay (§12.2): the room list, the active
/// highlight, and the switch/new/edit/settings commands. Handles the §10 focus-friction prompt.
/// </summary>
public partial class SwitcherViewModel : ObservableObject, IDisposable
{
    private readonly IRoomManager _rooms;
    private readonly ISwitchOrchestrator _orchestrator;

    public SwitcherViewModel(IRoomManager rooms, ISwitchOrchestrator orchestrator, FocusBarViewModel focusBar)
    {
        _rooms = rooms;
        _orchestrator = orchestrator;
        FocusBar = focusBar;

        _orchestrator.ActiveRoomChanged += OnActiveRoomChanged;
        Refresh();
    }

    public ObservableCollection<RoomItemViewModel> Rooms { get; } = new();

    public FocusBarViewModel FocusBar { get; }

    [ObservableProperty]
    private RoomItemViewModel? _activeRoom;

    /// <summary>Raised after a successful switch so a popup/overlay can close itself.</summary>
    public event EventHandler? CloseRequested;

    public event EventHandler? NewRoomRequested;

    public event EventHandler<Guid>? EditRoomRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? FocusSetupRequested;

    public void Refresh() => UiThread.Post(() =>
    {
        Rooms.Clear();
        foreach (var room in _rooms.Rooms.OrderBy(r => r.OrderIndex).ThenBy(r => r.Name))
            Rooms.Add(new RoomItemViewModel(room) { IsActive = _rooms.ActiveRoom?.Id == room.Id });

        ActiveRoom = Rooms.FirstOrDefault(r => r.IsActive);
    });

    private void OnActiveRoomChanged(object? sender, RoomActivatedEventArgs e) => UiThread.Post(() =>
    {
        foreach (var room in Rooms)
            room.IsActive = e.Room?.Id == room.Id;

        ActiveRoom = Rooms.FirstOrDefault(r => r.IsActive);
    });

    [RelayCommand]
    private async Task SwitchAsync(RoomItemViewModel? room)
    {
        if (room is null)
            return;

        var result = await _orchestrator.SwitchToAsync(room.Id);

        if (result.BlockedByFocus)
        {
            var remaining = FocusBarViewModel.Format(result.FocusRemaining ?? TimeSpan.Zero);
            var answer = System.Windows.MessageBox.Show(
                $"A focus session is still running ({remaining} left).\n\nEnd it early and switch anyway?",
                "Rooms – Focus session",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (answer != System.Windows.MessageBoxResult.Yes)
                return;

            await _orchestrator.SwitchToAsync(room.Id, endFocusSession: true);
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ResetActiveRoomAsync()
    {
        var active = _rooms.ActiveRoom;
        if (active is null)
            return;

        await _orchestrator.ResetRoomAsync(active.Id);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ShowAllAsync()
    {
        await _orchestrator.ShowAllAsync();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void NewRoom() => NewRoomRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void EditRoom(RoomItemViewModel? room)
    {
        if (room is not null)
            EditRoomRequested?.Invoke(this, room.Id);
    }

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void StartFocus() => FocusSetupRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _orchestrator.ActiveRoomChanged -= OnActiveRoomChanged;
}
