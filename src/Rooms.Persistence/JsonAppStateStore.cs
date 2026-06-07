using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;
using Rooms.Persistence.Json;

namespace Rooms.Persistence;

/// <summary>Stores runtime state in state.json (§11.1): last active room + failsafe set.</summary>
public sealed class JsonAppStateStore : IAppStateStore
{
    private readonly string _path;
    private readonly ILogger<JsonAppStateStore> _logger;

    public JsonAppStateStore(JsonStoreOptions options, ILogger<JsonAppStateStore> logger)
    {
        Directory.CreateDirectory(options.RootDirectory);
        _path = Path.Combine(options.RootDirectory, "state.json");
        _logger = logger;
    }

    public async Task<AppState> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path))
            return new AppState();

        try
        {
            await using var stream = File.OpenRead(_path);
            var state = await JsonSerializer
                .DeserializeAsync<AppState>(stream, RoomsJson.Options, ct)
                .ConfigureAwait(false);

            return state ?? new AppState();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "state.json is unreadable; using fresh state.");
            return new AppState();
        }
    }

    public Task SaveAsync(AppState state, CancellationToken ct = default) =>
        AtomicFile.WriteAsync(
            _path,
            stream => JsonSerializer.SerializeAsync(stream, state, RoomsJson.Options, ct),
            ct);
}
