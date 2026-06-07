using Rooms.Core.Models;

namespace Rooms.Core.Abstractions;

/// <summary>Durable storage for runtime state (state.json): last active room + failsafe set.</summary>
public interface IAppStateStore
{
    Task<AppState> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(AppState state, CancellationToken ct = default);
}
