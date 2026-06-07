namespace Rooms.Core.Models;

/// <summary>A live focus session: the room being focused on, when it started, how long. (§4.5)</summary>
public sealed class FocusSession
{
    public Guid RoomId { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public TimeSpan Duration { get; init; }

    /// <summary>Friction toggle: whether the user may exit before the timer ends.</summary>
    public bool AllowEarlyExit { get; init; }

    public DateTimeOffset EndsAt => StartedAt + Duration;
}
