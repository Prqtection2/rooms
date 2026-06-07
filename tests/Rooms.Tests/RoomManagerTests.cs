using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Rooms.Application.Rooms;
using Rooms.Core.Models;
using Rooms.Tests.Fakes;

namespace Rooms.Tests;

public class RoomManagerTests
{
    private readonly InMemoryRoomStore _store = new();

    private RoomManager CreateManager() => new(_store, NullLogger<RoomManager>.Instance);

    [Fact]
    public async Task CreateAsync_assigns_an_id_persists_and_tracks_the_room()
    {
        var manager = CreateManager();

        var room = await manager.CreateAsync("Focus");

        room.Id.Should().NotBe(Guid.Empty);
        room.Name.Should().Be("Focus");
        manager.Rooms.Should().ContainSingle().Which.Id.Should().Be(room.Id);
        _store.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task InitializeAsync_loads_rooms_ordered_by_order_index()
    {
        _store.Seed(
            new Room { Id = Guid.NewGuid(), Name = "B", OrderIndex = 1 },
            new Room { Id = Guid.NewGuid(), Name = "A", OrderIndex = 0 });
        var manager = CreateManager();

        await manager.InitializeAsync();

        manager.Rooms.Select(r => r.Name).Should().ContainInOrder("A", "B");
    }

    [Fact]
    public void SetActiveRoom_raises_ActiveRoomChanged_once()
    {
        var manager = CreateManager();
        var room = new Room { Id = Guid.NewGuid(), Name = "Focus" };
        var raised = 0;
        Room? reported = null;
        manager.ActiveRoomChanged += (_, e) => { raised++; reported = e.Room; };

        manager.SetActiveRoom(room);
        manager.SetActiveRoom(room); // same instance - should not re-raise

        raised.Should().Be(1);
        reported.Should().BeSameAs(room);
        manager.ActiveRoom.Should().BeSameAs(room);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_blank_name()
    {
        var manager = CreateManager();

        var act = async () => await manager.CreateAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_trims_the_name()
    {
        var manager = CreateManager();

        var room = await manager.CreateAsync("  Focus  ");

        room.Name.Should().Be("Focus");
    }

    [Fact]
    public async Task DeleteAsync_clears_active_room_when_the_active_room_is_deleted()
    {
        var manager = CreateManager();
        var room = await manager.CreateAsync("Focus");
        manager.SetActiveRoom(room);

        await manager.DeleteAsync(room.Id);

        manager.Rooms.Should().BeEmpty();
        manager.ActiveRoom.Should().BeNull();
    }
}
