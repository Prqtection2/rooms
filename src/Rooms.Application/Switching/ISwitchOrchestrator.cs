using Rooms.Application.Rooms;

namespace Rooms.Application.Switching;

/// <summary>
/// Coordinates the act of switching rooms (§6.2 / §7.2). This is the performance-critical path -
/// it must feel instantaneous.
/// </summary>
public interface ISwitchOrchestrator
{
    /// <param name="endFocusSession">When true, end an active locked focus session and switch
    /// anyway (the user's "give up", §10). When false, a locked session blocks the switch.</param>
    Task<SwitchResult> SwitchToAsync(Guid roomId, bool endFocusSession = false, CancellationToken ct = default);

    /// <summary>Reset a room to its defaults: forget session-added windows, relaunch the room's
    /// auto-launch apps, and hide everything that isn't part of the room.</summary>
    Task<SwitchResult> ResetRoomAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>Leave all rooms: re-show every hidden window and clear the active room.</summary>
    Task ShowAllAsync(CancellationToken ct = default);

    event EventHandler<RoomActivatedEventArgs>? ActiveRoomChanged;
}
