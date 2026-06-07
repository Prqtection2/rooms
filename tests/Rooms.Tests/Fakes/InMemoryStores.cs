using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Tests.Fakes;

/// <summary>In-memory <see cref="IRoomStore"/> for testing services without touching disk.</summary>
public sealed class InMemoryRoomStore : IRoomStore
{
    private List<Room> _rooms = new();

    public int SaveCount { get; private set; }

    public void Seed(params Room[] rooms) => _rooms = rooms.ToList();

    public Task<IReadOnlyList<Room>> LoadAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Room>>(_rooms.ToList());

    public Task SaveAsync(IReadOnlyList<Room> rooms, CancellationToken ct = default)
    {
        _rooms = rooms.ToList();
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory <see cref="IAppSettingsStore"/> for testing.</summary>
public sealed class InMemoryAppSettingsStore : IAppSettingsStore
{
    public AppSettings Settings { get; set; } = new();

    public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(Settings);

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory <see cref="IAppStateStore"/> for testing.</summary>
public sealed class InMemoryAppStateStore : IAppStateStore
{
    public AppState State { get; set; } = new();
    public int SaveCount { get; private set; }

    public Task<AppState> LoadAsync(CancellationToken ct = default) => Task.FromResult(State);

    public Task SaveAsync(AppState state, CancellationToken ct = default)
    {
        State = state;
        SaveCount++;
        return Task.CompletedTask;
    }
}
