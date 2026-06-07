namespace Rooms.Core.Models;

/// <summary>
/// Runtime state persisted across sessions (state.json, §11.1): the last active room and the
/// lost-window failsafe set. Separate from settings so it can be written frequently and is
/// safe to lose (defaults are harmless).
/// </summary>
public sealed class AppState
{
    public int SchemaVersion { get; set; } = 1;

    public Guid? LastActiveRoomId { get; set; }

    /// <summary>Handles of windows hidden by Rooms, so a crash can't strand them (§6 failsafe).</summary>
    public List<long> HiddenWindowHandles { get; set; } = new();
}
