using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Rooms.Core.Models;
using Rooms.Persistence;

namespace Rooms.Tests;

/// <summary>Exercises the real JSON room store (single versioned rooms.json) against a temp dir.</summary>
public sealed class JsonRoomStoreTests : IDisposable
{
    private readonly string _root;
    private readonly JsonRoomStore _store;

    public JsonRoomStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rooms-tests-" + Guid.NewGuid().ToString("n"));
        _store = new JsonRoomStore(new JsonStoreOptions { RootDirectory = _root }, NullLogger<JsonRoomStore>.Instance);
    }

    [Fact]
    public async Task Load_returns_empty_when_no_file_exists()
    {
        (await _store.LoadAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Save_then_Load_round_trips_rooms_with_nested_config()
    {
        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = "Deep Work",
            IconKey = "brain",
            AccentColorHex = "#3B82F6",
        };
        room.AutoLaunchApps.Add(new AppLaunchSpec
        {
            ExecutablePath = @"C:\tools\code.exe",
            LaunchOnEnter = true,
            DesiredPlacement = new WindowPlacement { Width = 800, Height = 600, ShowState = WindowShowState.Maximized },
        });
        room.OwnedWindowMatchers.Add(new WindowMatcher { ProcessName = "code", TitleRegex = @"\.cs$" });
        room.Rules.BlockingEnabled = true;
        room.Rules.Mode = BlockMode.Allowlist;
        room.Rules.Reaction = BlockReaction.MinimizeWindow;
        room.Rules.BlockedDomains.Add("reddit.com");
        room.Ambient.EnableDoNotDisturb = true;

        await _store.SaveAsync(new[] { room });
        var loaded = await _store.LoadAsync();

        loaded.Should().ContainSingle();
        var back = loaded[0];
        back.Name.Should().Be("Deep Work");
        back.AutoLaunchApps[0].DesiredPlacement!.ShowState.Should().Be(WindowShowState.Maximized);
        back.OwnedWindowMatchers[0].ProcessName.Should().Be("code");
        back.Rules.Mode.Should().Be(BlockMode.Allowlist);
        back.Rules.BlockedDomains.Should().Contain("reddit.com");
        back.Ambient.EnableDoNotDisturb.Should().BeTrue();
    }

    [Fact]
    public async Task Save_replaces_the_whole_collection()
    {
        await _store.SaveAsync(new[] { new Room { Id = Guid.NewGuid(), Name = "A" } });
        await _store.SaveAsync(new[]
        {
            new Room { Id = Guid.NewGuid(), Name = "B" },
            new Room { Id = Guid.NewGuid(), Name = "C" },
        });

        var loaded = await _store.LoadAsync();

        loaded.Select(r => r.Name).Should().BeEquivalentTo("B", "C");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
