using Rooms.Core.Models;

namespace Rooms.Application.Rooms;

/// <summary>Raised when the active room changes (including to null when none is active).</summary>
public sealed class RoomActivatedEventArgs(Room? room) : EventArgs
{
    public Room? Room { get; } = room;
}
