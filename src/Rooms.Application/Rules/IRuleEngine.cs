using Rooms.Core.Models;

namespace Rooms.Application.Rules;

/// <summary>
/// Pure, side-effect-free evaluation of window membership and blocking decisions for a room.
/// No Win32 - operates entirely on <see cref="WindowInfo"/> + the room's matchers/rules.
/// </summary>
public interface IRuleEngine
{
    /// <summary>True if the window matches one of the room's owned-window matchers (§4.3).</summary>
    bool IsOwnedBy(WindowInfo window, Room room);

    /// <summary>True if the window may stay visible while the room is active: it is owned by
    /// the room, or the room's blocking rules permit its process (§4.4).</summary>
    bool IsAllowed(WindowInfo window, Room room);

    /// <summary>The reaction to apply to a disallowed window, or null if it is allowed
    /// (or blocking is disabled).</summary>
    BlockReaction? GetBlockReaction(WindowInfo window, Room room);
}
