using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;
using Rooms.Persistence.Json;

namespace Rooms.Persistence;

/// <summary>Stores global settings as a single {root}\settings.json file.</summary>
public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private readonly string _path;
    private readonly ILogger<JsonAppSettingsStore> _logger;

    public JsonAppSettingsStore(JsonStoreOptions options, ILogger<JsonAppSettingsStore> logger)
    {
        Directory.CreateDirectory(options.RootDirectory);
        _path = Path.Combine(options.RootDirectory, "settings.json");
        _logger = logger;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path))
            return new AppSettings();

        try
        {
            await using var stream = File.OpenRead(_path);
            var settings = await JsonSerializer
                .DeserializeAsync<AppSettings>(stream, RoomsJson.Options, ct)
                .ConfigureAwait(false);

            return settings ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "Settings file unreadable; falling back to defaults.");
            return new AppSettings();
        }
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default) =>
        AtomicFile.WriteAsync(
            _path,
            stream => JsonSerializer.SerializeAsync(stream, settings, RoomsJson.Options, ct),
            ct);
}
