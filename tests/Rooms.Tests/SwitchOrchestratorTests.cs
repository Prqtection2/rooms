using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Rooms.Application.Focus;
using Rooms.Application.Rooms;
using Rooms.Application.Rules;
using Rooms.Application.Switching;
using Rooms.Application.Windows;
using Rooms.Core.Models;
using Rooms.Tests.Fakes;

namespace Rooms.Tests;

public class SwitchOrchestratorTests
{
    [Fact]
    public async Task Switching_to_an_unknown_room_fails_cleanly()
    {
        var harness = await Harness.CreateAsync();

        var result = await harness.Orchestrator.SwitchToAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Switching_shows_hidden_owned_windows_and_brings_the_primary_forward()
    {
        var room = RoomOwning("code");
        var windows = new[] { TestWindows.Make(1, "code") };
        var harness = await Harness.CreateAsync(new[] { room }, windows);
        harness.Windows.Hide((IntPtr)1); // we previously hid it (now in HiddenWindows, not visible)

        var result = await harness.Orchestrator.SwitchToAsync(room.Id);

        result.Success.Should().BeTrue();
        result.WindowsShown.Should().Be(1);
        harness.Windows.Shown.Should().Contain((IntPtr)1);
        harness.Windows.Foregrounded.Should().Contain((IntPtr)1);
        harness.Rooms.ActiveRoom.Should().BeSameAs(room);
        harness.RuleEnforcer.ActiveRoom.Should().BeSameAs(room);
    }

    [Fact]
    public async Task Switching_hides_a_window_that_belongs_to_another_room_and_persists_state()
    {
        var target = RoomOwning("code");
        var other = RoomOwning("chrome");
        var windows = new[] { TestWindows.Make(2, "chrome") };
        var harness = await Harness.CreateAsync(new[] { target, other }, windows);

        await harness.Orchestrator.SwitchToAsync(target.Id);

        harness.Windows.Hidden.Should().Contain((IntPtr)2);
        harness.State.State.HiddenWindowHandles.Should().Contain(2L);
        harness.State.State.LastActiveRoomId.Should().Be(target.Id);
    }

    [Fact]
    public async Task Switching_hides_a_minimized_loose_window_so_it_leaves_the_taskbar()
    {
        var target = RoomOwning("code");
        // A minimised app (Discord/Spotify left minimised) is "not visible" but still owns a taskbar
        // button - it must still be hidden when it doesn't belong to the room you're entering.
        var windows = new[] { TestWindows.Make(9, "spotify", isVisible: false, isMinimized: true) };
        var harness = await Harness.CreateAsync(new[] { target }, windows);
        harness.Settings.Settings = new AppSettings { UnassignedPolicy = UnassignedWindowPolicy.CurrentRoom };

        await harness.Orchestrator.SwitchToAsync(target.Id);

        harness.Windows.Hidden.Should().Contain((IntPtr)9);
    }

    [Fact]
    public async Task Sticky_policy_keeps_unassigned_windows_visible_in_every_room()
    {
        var room = RoomOwning("code");
        var windows = new[] { TestWindows.Make(9, "spotify") };
        var harness = await Harness.CreateAsync(new[] { room }, windows);
        harness.Settings.Settings = new AppSettings { UnassignedPolicy = UnassignedWindowPolicy.Sticky };

        await harness.Orchestrator.SwitchToAsync(room.Id);

        harness.Windows.Hidden.Should().BeEmpty();
    }

    [Fact]
    public async Task Current_room_policy_hides_unassigned_windows()
    {
        var room = RoomOwning("code");
        var windows = new[] { TestWindows.Make(9, "spotify") };
        var harness = await Harness.CreateAsync(new[] { room }, windows);
        harness.Settings.Settings = new AppSettings { UnassignedPolicy = UnassignedWindowPolicy.CurrentRoom };

        await harness.Orchestrator.SwitchToAsync(room.Id);

        harness.Windows.Hidden.Should().Contain((IntPtr)9);
    }

    [Fact]
    public async Task Global_sticky_process_names_are_never_hidden()
    {
        var room = RoomOwning("code");
        var windows = new[] { TestWindows.Make(9, "obs") };
        var harness = await Harness.CreateAsync(new[] { room }, windows);
        harness.Settings.Settings = new AppSettings
        {
            UnassignedPolicy = UnassignedWindowPolicy.CurrentRoom, // would otherwise hide it
            GlobalStickyProcessNames = { "obs" },
        };

        await harness.Orchestrator.SwitchToAsync(room.Id);

        harness.Windows.Hidden.Should().BeEmpty();
    }

    [Fact]
    public async Task Home_room_shows_loose_windows_but_hides_windows_owned_by_other_rooms()
    {
        var home = new Room { Id = Guid.NewGuid(), Name = "Home", IsCatchAll = true };
        var writing = RoomOwning("notepad");
        var windows = new[]
        {
            TestWindows.Make(1, "chrome"),  // loose / unassigned
            TestWindows.Make(2, "notepad"), // belongs to the Writing room
        };
        var harness = await Harness.CreateAsync(new[] { home, writing }, windows);
        harness.Registry.AssignManual((IntPtr)2, writing.Id);

        await harness.Orchestrator.SwitchToAsync(home.Id);

        harness.Windows.Hidden.Should().Contain((IntPtr)2);    // notepad (owned by Writing) hidden
        harness.Windows.Hidden.Should().NotContain((IntPtr)1); // chrome (loose) stays visible
    }

    [Fact]
    public async Task Switching_applies_ambient_and_website_rules()
    {
        var room = RoomOwning("code");
        room.Ambient.WallpaperPath = @"C:\wp\focus.jpg";
        room.Ambient.EnableDoNotDisturb = true;
        room.Rules.BlockedDomains.Add("reddit.com");
        var harness = await Harness.CreateAsync(new[] { room }, Array.Empty<WindowInfo>());

        await harness.Orchestrator.SwitchToAsync(room.Id);

        harness.Wallpaper.Applied.Should().Contain(@"C:\wp\focus.jpg");
        harness.Notifications.DoNotDisturb.Should().BeTrue();
        harness.WebsiteBlocker.Applied.Should().ContainSingle().Which.Should().Contain("reddit.com");
    }

    [Fact]
    public async Task Switching_launches_LaunchOnEnter_apps_that_are_not_running()
    {
        var room = RoomOwning("code");
        room.AutoLaunchApps.Add(new AppLaunchSpec { ExecutablePath = @"C:\apps\slack.exe", LaunchOnEnter = true });
        var harness = await Harness.CreateAsync(new[] { room }, new[] { TestWindows.Make(1, "code") });

        var result = await harness.Orchestrator.SwitchToAsync(room.Id);

        result.AppsLaunched.Should().Be(1);
        harness.Processes.Launched.Should().ContainSingle().Which.Should().Be(@"C:\apps\slack.exe");
    }

    // ----- §10 focus friction -----

    [Fact]
    public async Task Switching_away_during_a_locked_focus_session_is_blocked()
    {
        var focusRoom = RoomOwning("code");
        var other = RoomOwning("chrome");
        var harness = await Harness.CreateAsync(new[] { focusRoom, other }, new[] { TestWindows.Make(2, "chrome") });
        harness.Focus.Start(focusRoom.Id, TimeSpan.FromMinutes(25), allowEarlyExit: false);

        var result = await harness.Orchestrator.SwitchToAsync(other.Id);

        result.Success.Should().BeFalse();
        result.BlockedByFocus.Should().BeTrue();
        result.FocusRemaining.Should().NotBeNull();
        harness.Windows.Hidden.Should().BeEmpty(); // nothing changed
        harness.Focus.Current.Should().NotBeNull();
    }

    [Fact]
    public async Task Giving_up_ends_the_focus_session_and_completes_the_switch()
    {
        var focusRoom = RoomOwning("code");
        var other = RoomOwning("chrome");
        var harness = await Harness.CreateAsync(new[] { focusRoom, other }, Array.Empty<WindowInfo>());
        harness.Focus.Start(focusRoom.Id, TimeSpan.FromMinutes(25), allowEarlyExit: false);

        var result = await harness.Orchestrator.SwitchToAsync(other.Id, endFocusSession: true);

        result.Success.Should().BeTrue();
        harness.Focus.Current.Should().BeNull();
        harness.Rooms.ActiveRoom.Should().BeSameAs(other);
    }

    [Fact]
    public async Task Switching_to_the_focus_room_itself_is_always_allowed()
    {
        var focusRoom = RoomOwning("code");
        var harness = await Harness.CreateAsync(new[] { focusRoom }, Array.Empty<WindowInfo>());
        harness.Focus.Start(focusRoom.Id, TimeSpan.FromMinutes(25), allowEarlyExit: false);

        var result = await harness.Orchestrator.SwitchToAsync(focusRoom.Id);

        result.Success.Should().BeTrue();
        harness.Focus.Current.Should().NotBeNull(); // session preserved
    }

    [Fact]
    public async Task Auto_launch_does_not_relaunch_when_a_window_matches_by_process_name()
    {
        // Spec path is a bare name; the open window reports a full path + process name.
        var room = RoomOwning("notepad");
        room.AutoLaunchApps.Add(new AppLaunchSpec { ExecutablePath = "notepad.exe", LaunchOnEnter = true });
        var windows = new[]
        {
            TestWindows.Make(1, "notepad", executablePath: @"C:\Windows\System32\notepad.exe"),
        };
        var harness = await Harness.CreateAsync(new[] { room }, windows);

        var result = await harness.Orchestrator.SwitchToAsync(room.Id);

        result.AppsLaunched.Should().Be(0);
        harness.Processes.Launched.Should().BeEmpty();
    }

    [Fact]
    public async Task Isolated_app_launches_its_own_instance_even_if_open_in_another_room()
    {
        // A browser window already exists, owned by another room (explicit assignment).
        var target = new Room { Id = Guid.NewGuid(), Name = "Research" };
        target.AutoLaunchApps.Add(new AppLaunchSpec { ExecutablePath = "msedge", LaunchOnEnter = true, IsolatedInstance = true });
        var windows = new[] { TestWindows.Make(7, "msedge", executablePath: "msedge.exe") };
        var harness = await Harness.CreateAsync(new[] { target }, windows);

        // Pretend the existing Edge belongs to some other room.
        harness.Registry.AssignManual((IntPtr)7, Guid.NewGuid());

        var result = await harness.Orchestrator.SwitchToAsync(target.Id);

        result.AppsLaunched.Should().Be(1); // launched its own, despite Edge being open elsewhere
        harness.Processes.LaunchedIsolated.Should().ContainSingle()
            .Which.Key.Should().Be(target.Id.ToString("n"));
    }

    // ----- §15 idempotency / property checks -----

    [Fact]
    public async Task Switching_to_the_active_room_again_is_a_no_op()
    {
        var a = RoomOwning("code");
        var b = RoomOwning("chrome");
        var windows = new[] { TestWindows.Make(1, "code"), TestWindows.Make(2, "chrome") };
        var harness = await Harness.CreateAsync(new[] { a, b }, windows);

        await harness.Orchestrator.SwitchToAsync(a.Id);
        var second = await harness.Orchestrator.SwitchToAsync(a.Id);

        second.WindowsShown.Should().Be(0);
        second.WindowsHidden.Should().Be(0);
    }

    [Fact]
    public async Task Switching_A_to_B_back_to_A_restores_the_exact_visible_set()
    {
        var a = RoomOwning("code");
        var b = RoomOwning("chrome");
        var windows = new[]
        {
            TestWindows.Make(1, "code"),
            TestWindows.Make(2, "chrome"),
            TestWindows.Make(3, "spotify"), // unassigned + sticky -> always visible
        };
        var harness = await Harness.CreateAsync(new[] { a, b }, windows);
        harness.Settings.Settings = new AppSettings { UnassignedPolicy = UnassignedWindowPolicy.Sticky };

        await harness.Orchestrator.SwitchToAsync(a.Id);
        var afterFirstA = harness.Windows.VisibleSet();

        await harness.Orchestrator.SwitchToAsync(b.Id);
        await harness.Orchestrator.SwitchToAsync(a.Id);
        var afterSecondA = harness.Windows.VisibleSet();

        afterSecondA.Should().Equal(afterFirstA);
    }

    [Fact]
    public async Task File_Explorer_windows_are_roomed_and_not_treated_as_global_shell()
    {
        // A File Explorer window runs in explorer.exe (process "explorer") - the same process as the
        // desktop/taskbar - but it's a normal app window that should be hidden when it isn't part of
        // the room. (The desktop/taskbar themselves are filtered out earlier by window class.)
        var room = RoomOwning("code");
        var windows = new[] { TestWindows.Make(5, "explorer") };
        var harness = await Harness.CreateAsync(new[] { room }, windows);
        harness.Settings.Settings = new AppSettings { UnassignedPolicy = UnassignedWindowPolicy.CurrentRoom };

        await harness.Orchestrator.SwitchToAsync(room.Id);

        harness.Windows.Hidden.Should().Contain((IntPtr)5);
    }

    [Fact]
    public async Task Hidden_windows_are_reshown_back_to_front_to_preserve_z_order()
    {
        var home = new Room { Id = Guid.NewGuid(), Name = "Home", IsCatchAll = true };
        // EnumWindows reports windows top-of-z-order first.
        var windows = new[]
        {
            TestWindows.Make(1, "top"),
            TestWindows.Make(2, "mid"),
            TestWindows.Make(3, "bottom"),
        };
        var harness = await Harness.CreateAsync(new[] { home }, windows);
        harness.Windows.Hide((IntPtr)1);
        harness.Windows.Hide((IntPtr)2);
        harness.Windows.Hide((IntPtr)3);

        await harness.Orchestrator.SwitchToAsync(home.Id);

        // Re-shown bottom-first, so the originally-topmost window (1) is shown last and ends on top.
        harness.Windows.Shown.Should().Equal((IntPtr)3, (IntPtr)2, (IntPtr)1);
    }

    private static Room RoomOwning(string processName)
    {
        var room = new Room { Id = Guid.NewGuid(), Name = processName };
        room.OwnedWindowMatchers.Add(new WindowMatcher { ProcessName = processName });
        return room;
    }

    private sealed class Harness
    {
        public required RoomManager Rooms { get; init; }
        public required FakeWindowService Windows { get; init; }
        public required FakeProcessService Processes { get; init; }
        public required WindowRegistry Registry { get; init; }
        public required FakeRuleEnforcer RuleEnforcer { get; init; }
        public required FocusSessionService Focus { get; init; }
        public required FakeWallpaperService Wallpaper { get; init; }
        public required FakeNotificationService Notifications { get; init; }
        public required FakeWebsiteBlocker WebsiteBlocker { get; init; }
        public required InMemoryAppStateStore State { get; init; }
        public required InMemoryAppSettingsStore Settings { get; init; }
        public required SwitchOrchestrator Orchestrator { get; init; }

        public static async Task<Harness> CreateAsync(Room[]? rooms = null, IEnumerable<WindowInfo>? windows = null)
        {
            var store = new InMemoryRoomStore();
            if (rooms is not null)
                store.Seed(rooms);

            var manager = new RoomManager(store, NullLogger<RoomManager>.Instance);
            await manager.InitializeAsync();

            var fakeWindows = new FakeWindowService(windows);
            var processes = new FakeProcessService();
            var registry = new WindowRegistry();
            var ruleEnforcer = new FakeRuleEnforcer();
            var focus = new FocusSessionService(NullLogger<FocusSessionService>.Instance);
            var wallpaper = new FakeWallpaperService();
            var notifications = new FakeNotificationService();
            var websiteBlocker = new FakeWebsiteBlocker();
            var state = new InMemoryAppStateStore();
            var settings = new InMemoryAppSettingsStore();

            var orchestrator = new SwitchOrchestrator(
                manager,
                fakeWindows,
                processes,
                new RuleEngine(),
                registry,
                new SwitchGate(),
                ruleEnforcer,
                focus,
                wallpaper,
                notifications,
                websiteBlocker,
                state,
                settings,
                NullLogger<SwitchOrchestrator>.Instance);

            return new Harness
            {
                Rooms = manager,
                Windows = fakeWindows,
                Processes = processes,
                Registry = registry,
                RuleEnforcer = ruleEnforcer,
                Focus = focus,
                Wallpaper = wallpaper,
                Notifications = notifications,
                WebsiteBlocker = websiteBlocker,
                State = state,
                Settings = settings,
                Orchestrator = orchestrator,
            };
        }
    }
}
