using Rooms.Core.Models;

namespace Rooms.Application.Focus;

/// <summary>
/// Starts/stops focus sessions and drives the countdown (§7.4). While a session is active it
/// raises <see cref="Tick"/> each second for the UI, and <see cref="SessionEnded"/> on expiry.
/// If <c>AllowEarlyExit</c> is false, the UI is expected to intercept switching away (§10).
/// </summary>
public interface IFocusSessionService
{
    FocusSession? Current { get; }

    /// <summary>Whether the active session permits leaving before the timer ends.</summary>
    bool CanLeaveEarly { get; }

    event EventHandler<FocusSession>? SessionStarted;

    event EventHandler<FocusSession>? SessionEnded;

    /// <summary>Raised when a timed session reaches its end on its own (the timer ran out), as
    /// opposed to being stopped early. Use this to congratulate/notify; <see cref="SessionEnded"/>
    /// still fires for both early-stop and natural completion.</summary>
    event EventHandler<FocusSession>? SessionCompleted;

    /// <summary>Raised roughly every second while a timed session runs, with the time remaining.</summary>
    event EventHandler<TimeSpan>? Tick;

    /// <summary>Start a focus session for a room, replacing any in progress.</summary>
    void Start(Guid roomId, TimeSpan duration, bool allowEarlyExit = true);

    /// <summary>End the current session, if any.</summary>
    void Stop();
}
