using FluentAssertions;
using Rooms.Application.Rules;
using Rooms.Core.Models;
using Rooms.Tests.Fakes;

namespace Rooms.Tests;

public class RuleEngineTests
{
    private readonly RuleEngine _engine = new();

    [Fact]
    public void IsOwnedBy_matches_by_process_name_case_insensitively()
    {
        var room = RoomWithMatchers(new WindowMatcher { ProcessName = "Code" });

        _engine.IsOwnedBy(TestWindows.Make(1, "code", "main.cs"), room).Should().BeTrue();
    }

    [Fact]
    public void IsOwnedBy_is_false_when_process_differs()
    {
        var room = RoomWithMatchers(new WindowMatcher { ProcessName = "code" });

        _engine.IsOwnedBy(TestWindows.Make(1, "chrome"), room).Should().BeFalse();
    }

    [Fact]
    public void Title_regex_matcher_narrows_ownership()
    {
        var room = RoomWithMatchers(new WindowMatcher { ProcessName = "chrome", TitleRegex = @"^Issue #\d+" });

        _engine.IsOwnedBy(TestWindows.Make(1, "chrome", "Issue #42 open"), room).Should().BeTrue();
        _engine.IsOwnedBy(TestWindows.Make(2, "chrome", "YouTube"), room).Should().BeFalse();
    }

    [Fact]
    public void Executable_path_matcher_is_supported()
    {
        var room = RoomWithMatchers(new WindowMatcher { ExecutablePath = @"C:\tools\code.exe" });

        _engine.IsOwnedBy(TestWindows.Make(1, "code", executablePath: @"C:\tools\code.exe"), room).Should().BeTrue();
        _engine.IsOwnedBy(TestWindows.Make(2, "code", executablePath: @"C:\other\code.exe"), room).Should().BeFalse();
    }

    [Fact]
    public void A_matcher_with_no_criteria_matches_nothing()
    {
        var room = RoomWithMatchers(new WindowMatcher());

        _engine.IsOwnedBy(TestWindows.Make(1, "anything"), room).Should().BeFalse();
    }

    [Fact]
    public void Allowlist_permits_only_listed_processes()
    {
        var room = RoomWithMatchers();
        room.Rules.BlockingEnabled = true;
        room.Rules.Mode = BlockMode.Allowlist;
        room.Rules.AllowedProcessNames.Add("Spotify");

        _engine.IsAllowed(TestWindows.Make(1, "spotify"), room).Should().BeTrue();
        _engine.IsAllowed(TestWindows.Make(2, "discord"), room).Should().BeFalse();
    }

    [Fact]
    public void Blocklist_blocks_only_listed_processes()
    {
        var room = RoomWithMatchers();
        room.Rules.BlockingEnabled = true;
        room.Rules.Mode = BlockMode.Blocklist;
        room.Rules.BlockedProcessNames.Add("discord");

        _engine.IsAllowed(TestWindows.Make(1, "discord"), room).Should().BeFalse();
        _engine.IsAllowed(TestWindows.Make(2, "notepad"), room).Should().BeTrue();
    }

    [Fact]
    public void Allowlist_never_blocks_the_shell_safe_list()
    {
        var room = RoomWithMatchers();
        room.Rules.BlockingEnabled = true;
        room.Rules.Mode = BlockMode.Allowlist;
        // "explorer" is not on the user's allow-list, but the safe-list must keep it allowed.

        _engine.IsAllowed(TestWindows.Make(1, "explorer"), room).Should().BeTrue();
        _engine.IsAllowed(TestWindows.Make(2, "randomapp"), room).Should().BeFalse();
    }

    [Fact]
    public void Blocking_disabled_allows_everything()
    {
        var room = RoomWithMatchers();
        room.Rules.BlockingEnabled = false;

        _engine.IsAllowed(TestWindows.Make(1, "discord"), room).Should().BeTrue();
    }

    [Fact]
    public void GetBlockReaction_returns_the_configured_reaction_for_disallowed_windows()
    {
        var room = RoomWithMatchers();
        room.Rules.BlockingEnabled = true;
        room.Rules.Mode = BlockMode.Blocklist;
        room.Rules.BlockedProcessNames.Add("discord");
        room.Rules.Reaction = BlockReaction.CloseProcess;

        _engine.GetBlockReaction(TestWindows.Make(1, "discord"), room).Should().Be(BlockReaction.CloseProcess);
        _engine.GetBlockReaction(TestWindows.Make(2, "notepad"), room).Should().BeNull();
    }

    private static Room RoomWithMatchers(params WindowMatcher[] matchers)
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Test" };
        room.OwnedWindowMatchers.AddRange(matchers);
        return room;
    }
}
