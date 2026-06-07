namespace Rooms.Core.Models;

/// <summary>Describes an app a room can auto-launch on entry. (§4.2)</summary>
public sealed class AppLaunchSpec
{
    public string ExecutablePath { get; set; } = string.Empty;

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    /// <summary>Start it when entering the room.</summary>
    public bool LaunchOnEnter { get; set; }

    /// <summary>
    /// Launch a separate instance owned by this room rather than reusing one open elsewhere.
    /// For browsers this means a per-room profile (its own window/tabs); single-instance apps
    /// that can't be isolated will simply focus their existing window.
    /// </summary>
    public bool IsolatedInstance { get; set; }

    /// <summary>Optional desired position/size for the launched window.</summary>
    public WindowPlacement? DesiredPlacement { get; set; }
}
