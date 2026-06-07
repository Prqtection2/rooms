using Rooms.Core.Models;

namespace Rooms.Application.Rooms;

/// <summary>
/// In-memory source of truth for the room collection and the currently active room,
/// backed by an <see cref="Core.Abstractions.IRoomStore"/>. CRUD only - the visual
/// switch itself is the job of <see cref="ISwitchOrchestrator"/>.
/// </summary>
public interface IRoomManager
{
    IReadOnlyList<Room> Rooms { get; }

    Room? ActiveRoom { get; }

    event EventHandler<RoomActivatedEventArgs>? ActiveRoomChanged;

    /// <summary>Load all rooms from the store into memory. Call once at startup.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    Room? Get(Guid roomId);

    Task<Room> CreateAsync(string name, CancellationToken ct = default);

    Task UpdateAsync(Room room, CancellationToken ct = default);

    Task DeleteAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>Set the active room and raise <see cref="ActiveRoomChanged"/>.
    /// Called by the switch orchestrator once a switch completes.</summary>
    void SetActiveRoom(Room? room);
}
