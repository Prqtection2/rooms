using Rooms.Core.Models;

namespace Rooms.Core.Abstractions;

/// <summary>Durable storage for the room collection (single rooms.json file, §11.1/§11.2).</summary>
public interface IRoomStore
{
    Task<IReadOnlyList<Room>> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(IReadOnlyList<Room> rooms, CancellationToken ct = default);
}
