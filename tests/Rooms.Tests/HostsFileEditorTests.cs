using FluentAssertions;
using Rooms.Os.Windows;

namespace Rooms.Tests;

public class HostsFileEditorTests
{
    private const string UserContent = "127.0.0.1 localhost\r\n::1 localhost";

    [Fact]
    public void ApplyManagedBlock_adds_a_delimited_block_pointing_domains_to_loopback()
    {
        var result = HostsFileEditor.ApplyManagedBlock(UserContent, new[] { "reddit.com" });

        result.Should().Contain("127.0.0.1 localhost");                  // user content preserved
        result.Should().Contain(HostsFileEditor.BeginMarker);
        result.Should().Contain(HostsFileEditor.EndMarker);
        result.Should().Contain("127.0.0.1 reddit.com");
        result.Should().Contain("127.0.0.1 www.reddit.com");            // www variant added
    }

    [Fact]
    public void RemoveManagedBlock_strips_the_block_but_keeps_user_entries()
    {
        var blocked = HostsFileEditor.ApplyManagedBlock(UserContent, new[] { "reddit.com" });

        var cleared = HostsFileEditor.RemoveManagedBlock(blocked);

        cleared.Should().Contain("127.0.0.1 localhost");
        cleared.Should().NotContain(HostsFileEditor.BeginMarker);
        cleared.Should().NotContain("reddit.com");
    }

    [Fact]
    public void ApplyManagedBlock_replaces_an_existing_block_without_duplicating()
    {
        var first = HostsFileEditor.ApplyManagedBlock(UserContent, new[] { "reddit.com" });
        var second = HostsFileEditor.ApplyManagedBlock(first, new[] { "twitter.com" });

        second.Split(HostsFileEditor.BeginMarker).Length.Should().Be(2); // exactly one marker
        second.Should().Contain("twitter.com");
        second.Should().NotContain("reddit.com");
    }

    [Fact]
    public void ApplyManagedBlock_with_no_domains_removes_the_block()
    {
        var blocked = HostsFileEditor.ApplyManagedBlock(UserContent, new[] { "reddit.com" });

        var cleared = HostsFileEditor.ApplyManagedBlock(blocked, Array.Empty<string>());

        cleared.Should().NotContain(HostsFileEditor.BeginMarker);
        cleared.Should().Contain("127.0.0.1 localhost");
    }
}
