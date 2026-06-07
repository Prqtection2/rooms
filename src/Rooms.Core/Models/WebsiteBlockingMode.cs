namespace Rooms.Core.Models;

/// <summary>Which website-blocking module is active (§9 / §13 - swappable).</summary>
public enum WebsiteBlockingMode
{
    /// <summary>Option A: rely on the room's process rules to close/minimise the browser. Reliable,
    /// no elevation. The default.</summary>
    BrowserGating,

    /// <summary>Option B: rewrite the hosts file (needs administrator rights).</summary>
    HostsFile,
}
