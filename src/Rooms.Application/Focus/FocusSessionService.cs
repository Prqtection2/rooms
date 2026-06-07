using Microsoft.Extensions.Logging;
using Rooms.Core.Models;

namespace Rooms.Application.Focus;

/// <inheritdoc cref="IFocusSessionService" />
public sealed class FocusSessionService : IFocusSessionService, IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger<FocusSessionService> _logger;
    private readonly object _lock = new();

    private FocusSession? _current;
    private Timer? _timer;

    public FocusSessionService(ILogger<FocusSessionService> logger)
    {
        _logger = logger;
    }

    public FocusSession? Current => _current;

    public bool CanLeaveEarly => _current?.AllowEarlyExit ?? true;

    public event EventHandler<FocusSession>? SessionStarted;

    public event EventHandler<FocusSession>? SessionEnded;

    public event EventHandler<TimeSpan>? Tick;

    public void Start(Guid roomId, TimeSpan duration, bool allowEarlyExit = true)
    {
        Stop();

        var session = new FocusSession
        {
            RoomId = roomId,
            StartedAt = DateTimeOffset.UtcNow,
            Duration = duration,
            AllowEarlyExit = allowEarlyExit,
        };

        lock (_lock)
        {
            _current = session;
            if (duration > TimeSpan.Zero)
                _timer = new Timer(_ => OnTick(), null, TickInterval, TickInterval);
        }

        _logger.LogInformation(
            "Focus session started for room {RoomId} ({Minutes:0.#} min, early exit: {AllowEarlyExit}).",
            roomId, duration.TotalMinutes, allowEarlyExit);

        SessionStarted?.Invoke(this, session);
    }

    public void Stop()
    {
        FocusSession? ended;
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            ended = _current;
            _current = null;
        }

        if (ended is null)
            return;

        _logger.LogInformation("Focus session ended for room {RoomId}.", ended.RoomId);
        SessionEnded?.Invoke(this, ended);
    }

    private void OnTick()
    {
        var session = _current;
        if (session is null)
            return;

        var remaining = session.EndsAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            Tick?.Invoke(this, TimeSpan.Zero);
            Stop();
            // TODO (focus milestone): optionally auto-switch to a configured "break" room.
            return;
        }

        Tick?.Invoke(this, remaining);
    }

    public void Dispose() => Stop();
}
