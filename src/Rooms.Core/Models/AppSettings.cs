namespace Rooms.Core.Models;

/// <summary>A hotkey bound to a named action, optionally targeting a specific room. (§11.3)</summary>
public sealed record HotkeyBinding(string ActionKey, HotkeyDefinition Hotkey, Guid? RoomId = null);

/// <summary>Global application settings (§11.3). Versioned for forward-compatible migration.</summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Hotkey bindings (switch-to-room, next/previous room, open switcher, start focus).</summary>
    public List<HotkeyBinding> Hotkeys { get; set; } = new();

    /// <summary>How windows belonging to no room are treated (§6.1). Default CurrentRoom makes
    /// rooms exclusive: entering a room shows only its apps and hides everything else.</summary>
    public UnassignedWindowPolicy UnassignedPolicy { get; set; } = UnassignedWindowPolicy.CurrentRoom;

    /// <summary>Default length (minutes) for a focus session started from the hotkey.</summary>
    public int DefaultFocusMinutes { get; set; } = 25;

    public bool RunAtStartup { get; set; }

    /// <summary>Opt-in (§10): during a locked focus session, hide windows that intrude on the
    /// focus room. Friction-based and bypassable - not a kernel-level lock.</summary>
    public bool FocusReassertion { get; set; }

    /// <summary>Room activated on launch when there is no last-active room.</summary>
    public Guid? DefaultRoomId { get; set; }

    /// <summary>Processes that stay visible in every room (in addition to the built-in safe-list).</summary>
    public List<string> GlobalStickyProcessNames { get; set; } = new();

    /// <summary>Which website-blocking module is active (§9 / §13).</summary>
    public WebsiteBlockingMode WebsiteBlocking { get; set; } = WebsiteBlockingMode.BrowserGating;
}
