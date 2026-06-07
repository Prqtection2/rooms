using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Rooms.Application.Rules;
using Rooms.Application.Switching;
using Rooms.Application.Windows;
using Rooms.Core.Models;
using Rooms.Tests.Fakes;

namespace Rooms.Tests;

public class RuleEnforcerTests
{
    private static (RuleEnforcer Enforcer, WindowRegistry Registry, FakeWindowService Windows) Build()
    {
        var registry = new WindowRegistry();
        var windows = new FakeWindowService();
        var enforcer = new RuleEnforcer(
            windows,
            new FakeProcessService(),
            new RuleEngine(),
            registry,
            new SwitchGate(),
            new FakeNotificationService(),
            NullLogger<RuleEnforcer>.Instance);
        enforcer.Start();
        return (enforcer, registry, windows);
    }

    [Fact]
    public void A_background_show_of_another_rooms_window_is_re_hidden_not_followed()
    {
        var (enforcer, registry, windows) = Build();
        var window = TestWindows.Make(5, "chrome");
        var otherRoom = Guid.NewGuid();
        registry.Adopt((IntPtr)5, otherRoom); // owned by another room

        var here = new Room { Id = Guid.NewGuid(), Name = "Here" };
        enforcer.SetActiveRoom(here);
        windows.RaiseWindowCreated(window); // it ticks/reveals itself in the background, not by the user

        registry.GetExplicitRoom(window).Should().Be(otherRoom);    // ownership unchanged
        windows.Hidden.Should().Contain((IntPtr)5);                 // hidden again to keep this room clean
    }

    [Fact]
    public void Activating_a_window_from_another_room_follows_it_in_and_re_attaches_its_taskbar()
    {
        var (enforcer, registry, windows) = Build();
        var window = TestWindows.Make(8, "slack");
        registry.Adopt((IntPtr)8, Guid.NewGuid()); // owned by another room, hidden

        var here = new Room { Id = Guid.NewGuid(), Name = "Here" };
        enforcer.SetActiveRoom(here);
        windows.RaiseWindowActivated(window); // user relaunched / Alt-Tabbed to it

        registry.GetExplicitRoom(window).Should().Be(here.Id);
        windows.Shown.Should().Contain((IntPtr)8); // re-shown so its taskbar button returns
    }

    [Fact]
    public void A_catch_all_room_does_not_steal_windows()
    {
        var (enforcer, registry, windows) = Build();
        var owner = Guid.NewGuid();
        var window = TestWindows.Make(5, "chrome");
        registry.Adopt((IntPtr)5, owner);

        enforcer.SetActiveRoom(new Room { Id = Guid.NewGuid(), Name = "Everything", IsCatchAll = true });
        windows.RaiseWindowCreated(window);

        registry.GetExplicitRoom(window).Should().Be(owner); // unchanged
    }

    [Fact]
    public void A_loose_window_brought_to_the_foreground_joins_the_active_room()
    {
        var (enforcer, registry, windows) = Build();
        registry.UnassignedPolicy = UnassignedWindowPolicy.CurrentRoom;
        var window = TestWindows.Make(6, "spotify");

        var here = new Room { Id = Guid.NewGuid(), Name = "Here" };
        enforcer.SetActiveRoom(here);
        windows.RaiseWindowActivated(window); // user clicked / opened it here

        registry.GetExplicitRoom(window).Should().Be(here.Id);
    }

    [Fact]
    public void A_loose_window_ticking_in_the_background_is_not_dragged_into_the_room()
    {
        var (enforcer, registry, windows) = Build();
        registry.UnassignedPolicy = UnassignedWindowPolicy.CurrentRoom;
        var window = TestWindows.Make(6, "spotify"); // a loose app, never hidden

        var here = new Room { Id = Guid.NewGuid(), Name = "Here" };
        enforcer.SetActiveRoom(here);
        windows.RaiseWindowCreated(window); // background show event (song change), not user-driven

        registry.GetExplicitRoom(window).Should().BeNull(); // stays loose; doesn't join the room
    }

    [Fact]
    public void A_hidden_loose_window_that_reveals_itself_is_hidden_again()
    {
        var (enforcer, registry, windows) = Build();
        var window = TestWindows.Make(7, "discord");
        windows.Hide((IntPtr)7); // we hid it on entering this focused room

        var here = new Room { Id = Guid.NewGuid(), Name = "Here" };
        enforcer.SetActiveRoom(here);
        windows.RaiseWindowCreated(window); // it un-hid itself (a notification arrived)

        windows.HiddenWindows.Should().Contain((IntPtr)7); // pushed back out of the room
    }
}
