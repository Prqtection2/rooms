using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Rooms.Application.Hotkeys;
using Rooms.Application.Rooms;
using Rooms.Application.Startup;
using Rooms.Core.Models;
using Rooms.Tests.Fakes;

namespace Rooms.Tests;

public class DefaultRoomsSeederTests
{
    private readonly InMemoryRoomStore _roomStore = new();
    private readonly InMemoryAppSettingsStore _settingsStore = new();
    private readonly FakeHotkeyService _hotkeys = new();
    private readonly RoomManager _rooms;

    public DefaultRoomsSeederTests()
    {
        _rooms = new RoomManager(_roomStore, NullLogger<RoomManager>.Instance);
    }

    private DefaultRoomsSeeder CreateSeeder() =>
        new(_rooms, _settingsStore, _hotkeys, NullLogger<DefaultRoomsSeeder>.Instance);

    [Fact]
    public async Task SeedIfEmptyAsync_seeds_home_notepad_edge_when_no_rooms_exist()
    {
        await _rooms.InitializeAsync();
        var seeder = CreateSeeder();

        var seeded = await seeder.SeedIfEmptyAsync();

        seeded.Should().BeTrue();
        _rooms.Rooms.Select(r => r.Name).Should().ContainInOrder("Home", "Notepad", "Microsoft Edge");

        var home = _rooms.Rooms.Single(r => r.Name == "Home");
        home.IsCatchAll.Should().BeTrue("Home must keep every loose window so nothing is ever lost");

        var notepad = _rooms.Rooms.Single(r => r.Name == "Notepad");
        notepad.IsCatchAll.Should().BeFalse();
        notepad.OwnedWindowMatchers.Should().ContainSingle().Which.ProcessName.Should().Be("notepad");
        notepad.AutoLaunchApps.Should().ContainSingle().Which.LaunchOnEnter.Should().BeTrue();
    }

    [Fact]
    public async Task SeedIfEmptyAsync_lands_in_Home_and_keeps_focused_rooms_clean()
    {
        await _rooms.InitializeAsync();
        var seeder = CreateSeeder();

        await seeder.SeedIfEmptyAsync();

        var home = _rooms.Rooms.Single(r => r.Name == "Home");
        // Default into Home so the user starts with everything they had open visible.
        _settingsStore.Settings.DefaultRoomId.Should().Be(home.Id);
        // CurrentRoom policy hides loose windows in Notepad/Edge so those rooms stay focused.
        _settingsStore.Settings.UnassignedPolicy.Should().Be(UnassignedWindowPolicy.CurrentRoom);
    }

    [Fact]
    public async Task SeedIfEmptyAsync_binds_the_starter_hotkeys_and_rearms_them()
    {
        await _rooms.InitializeAsync();
        var seeder = CreateSeeder();

        await seeder.SeedIfEmptyAsync();

        _settingsStore.Settings.Hotkeys.Should().HaveCount(5);
        _settingsStore.Settings.Hotkeys.Count(b => b.ActionKey == HotkeyActions.SwitchToRoom).Should().Be(3);
        _settingsStore.Settings.Hotkeys.Should().ContainSingle(b => b.ActionKey == HotkeyActions.OpenSwitcher);
        _settingsStore.Settings.Hotkeys.Should().ContainSingle(b => b.ActionKey == HotkeyActions.ToggleFocus);
        _hotkeys.ReloadCount.Should().BeGreaterThan(0, "newly-seeded bindings must be registered");
    }

    [Fact]
    public async Task SeedIfEmptyAsync_is_a_no_op_when_rooms_already_exist()
    {
        _roomStore.Seed(new Room { Id = Guid.NewGuid(), Name = "Mine" });
        await _rooms.InitializeAsync();
        var seeder = CreateSeeder();

        var seeded = await seeder.SeedIfEmptyAsync();

        seeded.Should().BeFalse();
        _rooms.Rooms.Should().ContainSingle().Which.Name.Should().Be("Mine");
    }

    [Fact]
    public async Task SeedAsync_does_not_duplicate_rooms_when_run_twice()
    {
        await _rooms.InitializeAsync();
        var seeder = CreateSeeder();

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        _rooms.Rooms.Where(r => r.Name == "Home").Should().ContainSingle();
        _rooms.Rooms.Where(r => r.Name == "Notepad").Should().ContainSingle();
        _rooms.Rooms.Where(r => r.Name == "Microsoft Edge").Should().ContainSingle();
    }
}
