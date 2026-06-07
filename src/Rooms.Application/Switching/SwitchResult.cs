namespace Rooms.Application.Switching;

/// <summary>Outcome of a room switch, surfaced to the UI for feedback and logging.</summary>
public sealed record SwitchResult(
    bool Success,
    Guid RoomId,
    int WindowsShown,
    int WindowsHidden,
    int AppsLaunched,
    bool BlockedByFocus = false,
    TimeSpan? FocusRemaining = null,
    string? Error = null)
{
    public static SwitchResult Failed(Guid roomId, string error) =>
        new(false, roomId, 0, 0, 0, Error: error);

    /// <summary>The switch was refused because a locked focus session is active (§10).</summary>
    public static SwitchResult BlockedByFocusSession(Guid roomId, TimeSpan remaining) =>
        new(false, roomId, 0, 0, 0, BlockedByFocus: true, FocusRemaining: remaining);
}
