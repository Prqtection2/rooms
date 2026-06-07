namespace Rooms.Core.Models;

/// <summary>Ambient environment a room applies on entry. (§4.6)</summary>
public sealed class AmbientSettings
{
    public string? WallpaperPath { get; set; }

    /// <summary>Toggle Focus Assist / Do Not Disturb (best-effort; see §5.4 notes).</summary>
    public bool EnableDoNotDisturb { get; set; }
}
