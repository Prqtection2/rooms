using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rooms.Application.Focus;
using Rooms.Application.Rooms;
using Rooms.Application.Switching;

namespace Rooms.App.ViewModels;

/// <summary>
/// "Start a focus session" dialog: choose which room to focus in, how long, and whether to lock
/// (switching away then asks to give up, §10). Starting switches into the room first.
/// </summary>
public partial class FocusSessionDialogViewModel : ObservableObject
{
    private readonly IFocusSessionService _focus;
    private readonly ISwitchOrchestrator _orchestrator;
    private readonly IRoomManager _roomManager;

    public FocusSessionDialogViewModel(
        IFocusSessionService focus, ISwitchOrchestrator orchestrator, IRoomManager roomManager, Guid? preselectRoomId = null)
    {
        _focus = focus;
        _orchestrator = orchestrator;
        _roomManager = roomManager;

        foreach (var room in _roomManager.Rooms.OrderBy(r => r.OrderIndex))
            Rooms.Add(new RoomChoice(room.Id, room.Name));

        // Default to the room the user is acting on (e.g. the one highlighted in the switcher),
        // falling back to the active room. This is what makes "focus on room 2" lock room 2.
        var targetId = preselectRoomId ?? _roomManager.ActiveRoom?.Id;
        SelectedRoom = Rooms.FirstOrDefault(c => c.Id == targetId) ?? Rooms.FirstOrDefault();
    }

    public ObservableCollection<RoomChoice> Rooms { get; } = new();

    [ObservableProperty]
    private RoomChoice? _selectedRoom;

    [ObservableProperty]
    private int _minutes = 25;

    [ObservableProperty]
    private bool _lockMe = true;

    public event EventHandler? Started;

    [RelayCommand]
    private void Preset(string minutes)
    {
        if (int.TryParse(minutes, out var value))
            Minutes = value;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (SelectedRoom?.Id is not Guid roomId)
            return;

        var minutes = Minutes > 0 ? Minutes : 25;

        _focus.Stop();                                       // clear any existing lock first
        await _orchestrator.SwitchToAsync(roomId);           // enter the room you're focusing in
        _focus.Start(roomId, TimeSpan.FromMinutes(minutes), allowEarlyExit: !LockMe);

        Started?.Invoke(this, EventArgs.Empty);
    }
}
