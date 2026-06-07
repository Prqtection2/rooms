using Rooms.Core.Models;

namespace Rooms.Core.Abstractions;

/// <summary>Durable storage for global application settings.</summary>
public interface IAppSettingsStore
{
    /// <summary>Load settings, returning defaults if none have been saved yet.</summary>
    Task<AppSettings> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
