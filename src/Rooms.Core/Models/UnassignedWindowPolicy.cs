namespace Rooms.Core.Models;

/// <summary>
/// What happens to windows the user opened that match no room (§6.1).
/// <list type="bullet">
/// <item><see cref="Sticky"/> - stay visible in every room (good for e.g. a volume window).</item>
/// <item><see cref="CurrentRoom"/> - get adopted by whatever room is active when they appear.</item>
/// </list>
/// </summary>
public enum UnassignedWindowPolicy
{
    Sticky,
    CurrentRoom,
}
