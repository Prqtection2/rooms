namespace Rooms.Core.Models;

/// <summary>Whether a room's block list names what's forbidden or what's permitted. (§4.4)</summary>
public enum BlockMode
{
    Blocklist,
    Allowlist,
}

/// <summary>What to do with a window/process that violates a room's rules. (§4.4)</summary>
public enum BlockReaction
{
    CloseProcess,
    MinimizeWindow,
    WarnOnly,
}

/// <summary>
/// Focus / enforcement rules for a room. Enforcement is friction-based, not a hard lock. (§4.4)
/// </summary>
public sealed class RoomRules
{
    public bool BlockingEnabled { get; set; }

    public BlockMode Mode { get; set; }

    public List<string> BlockedProcessNames { get; set; } = new();

    public List<string> AllowedProcessNames { get; set; } = new();

    /// <summary>Domains to block (see §9 caveats - best-effort).</summary>
    public List<string> BlockedDomains { get; set; } = new();

    public BlockReaction Reaction { get; set; }
}
