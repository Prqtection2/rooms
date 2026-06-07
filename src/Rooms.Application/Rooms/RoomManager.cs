using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Application.Rooms;

/// <inheritdoc cref="IRoomManager" />
public sealed class RoomManager : IRoomManager
{
    private readonly IRoomStore _store;
    private readonly ILogger<RoomManager> _logger;
    private readonly List<Room> _rooms = new();
    private Room? _active;

    public RoomManager(IRoomStore store, ILogger<RoomManager> logger)
    {
        _store = store;
        _logger = logger;
    }

    public IReadOnlyList<Room> Rooms => _rooms;

    public Room? ActiveRoom => _active;

    public event EventHandler<RoomActivatedEventArgs>? ActiveRoomChanged;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var loaded = await _store.LoadAsync(ct).ConfigureAwait(false);
        _rooms.Clear();
        _rooms.AddRange(loaded.OrderBy(r => r.OrderIndex).ThenBy(r => r.Name));
        _logger.LogInformation("Loaded {Count} room(s) from store.", _rooms.Count);
    }

    public Room? Get(Guid roomId) => _rooms.FirstOrDefault(r => r.Id == roomId);

    public async Task<Room> CreateAsync(string name, CancellationToken ct = default)
    {
        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = ValidateName(name),
            OrderIndex = _rooms.Count,
        };

        _rooms.Add(room);
        await _store.SaveAsync(_rooms, ct).ConfigureAwait(false);
        _logger.LogInformation("Created room {RoomId} ({Name}).", room.Id, room.Name);
        return room;
    }

    public async Task UpdateAsync(Room room, CancellationToken ct = default)
    {
        room.Name = ValidateName(room.Name);

        var index = _rooms.FindIndex(r => r.Id == room.Id);
        if (index >= 0)
            _rooms[index] = room;
        else
            _rooms.Add(room);

        await _store.SaveAsync(_rooms, ct).ConfigureAwait(false);
        _logger.LogInformation("Updated room {RoomId} ({Name}).", room.Id, room.Name);
    }

    public async Task DeleteAsync(Guid roomId, CancellationToken ct = default)
    {
        _rooms.RemoveAll(r => r.Id == roomId);
        await _store.SaveAsync(_rooms, ct).ConfigureAwait(false);

        if (_active?.Id == roomId)
            SetActiveRoom(null);

        _logger.LogInformation("Deleted room {RoomId}.", roomId);
    }

    public void SetActiveRoom(Room? room)
    {
        if (ReferenceEquals(_active, room))
            return;

        _active = room;
        ActiveRoomChanged?.Invoke(this, new RoomActivatedEventArgs(room));
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Room name must not be empty.", nameof(name));

        return name.Trim();
    }
}
