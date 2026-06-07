namespace Rooms.Core.Models;

/// <summary>
/// A room: a named, focused window environment. Configuration-as-data - portable,
/// editable, and serialisable to JSON. (§4.1)
/// </summary>
public sealed class Room
{
    /// <summary>Stable identity.</summary>
    public Guid Id { get; init; }

    public string Name { get; set; } = "New Room";

    /// <summary>Maps to a built-in icon.</summary>
    public string? IconKey { get; set; }

    /// <summary>Optional per-room accent colour (hex, e.g. "#3B82F6").</summary>
    public string? AccentColorHex { get; set; }

    /// <summary>Position in the switcher.</summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// A catch-all room: when active it keeps every window visible (hides nothing). Use it as a
    /// safe "home"/"everything" room so you can always get all your windows back.
    /// </summary>
    public bool IsCatchAll { get; set; }

    /// <summary>Apps this room can auto-launch on entry.</summary>
    public List<AppLaunchSpec> AutoLaunchApps { get; set; } = new();

    public RoomRules Rules { get; set; } = new();

    public AmbientSettings Ambient { get; set; } = new();

    /// <summary>
    /// Window assignment is partly dynamic (§6), but a room remembers which windows it
    /// "owns" by these matching heuristics.
    /// </summary>
    public List<WindowMatcher> OwnedWindowMatchers { get; set; } = new();
}
