using FluentAssertions;
using Rooms.Application.Windows;
using Rooms.Tests.Fakes;

namespace Rooms.Tests;

public class WindowRegistryTests
{
    private readonly WindowRegistry _registry = new();
    private readonly Guid _roomA = Guid.NewGuid();
    private readonly Guid _roomB = Guid.NewGuid();

    [Fact]
    public void Launched_windows_are_owned_by_the_launching_room_by_pid()
    {
        _registry.TrackLaunch(pid: 100, _roomA);
        var window = TestWindows.Make(1, "slack"); // pid == handle == 100? build explicit
        var win = window with { ProcessId = 100 };

        _registry.GetExplicitRoom(win).Should().Be(_roomA);
        _registry.GetSource(win).Should().Be(AssignmentSource.LaunchedByRoom);
    }

    [Fact]
    public void Manual_assignment_overrides_a_launch_assignment()
    {
        var win = TestWindows.Make(5, "code") with { ProcessId = 100 };
        _registry.TrackLaunch(100, _roomA);
        _registry.AssignManual((IntPtr)5, _roomB);

        _registry.GetExplicitRoom(win).Should().Be(_roomB);
        _registry.GetSource(win).Should().Be(AssignmentSource.ManualOverride);
    }

    [Fact]
    public void Adopt_assigns_an_unassigned_window_but_never_overrides_a_manual_one()
    {
        var win = TestWindows.Make(7, "notepad");
        _registry.Adopt((IntPtr)7, _roomA);
        _registry.GetExplicitRoom(win).Should().Be(_roomA);

        _registry.AssignManual((IntPtr)7, _roomB);
        _registry.Adopt((IntPtr)7, _roomA); // must not clobber the manual assignment
        _registry.GetExplicitRoom(win).Should().Be(_roomB);
    }

    [Fact]
    public void Unknown_windows_have_no_explicit_room()
    {
        var win = TestWindows.Make(9, "chrome");

        _registry.GetExplicitRoom(win).Should().BeNull();
        _registry.GetSource(win).Should().Be(AssignmentSource.Unassigned);
    }

    [Fact]
    public void Sticky_flag_is_tracked_and_cleared()
    {
        _registry.SetSticky((IntPtr)3, true);
        _registry.IsSticky((IntPtr)3).Should().BeTrue();

        _registry.SetSticky((IntPtr)3, false);
        _registry.IsSticky((IntPtr)3).Should().BeFalse();
    }

    [Fact]
    public void Forget_clears_manual_and_sticky_assignments()
    {
        var win = TestWindows.Make(4, "code");
        _registry.AssignManual((IntPtr)4, _roomA);
        _registry.SetSticky((IntPtr)4, true);

        _registry.Forget((IntPtr)4);

        _registry.GetExplicitRoom(win).Should().BeNull();
        _registry.IsSticky((IntPtr)4).Should().BeFalse();
    }
}
