using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;
using Rooms.Persistence.Json;

namespace Rooms.Persistence;

/// <summary>
/// Stores all rooms in a single versioned rooms.json (§11.1). Written atomically so a crash
/// mid-write can't corrupt the file, and wrapped in a schema-versioned envelope for migration.
/// </summary>
public sealed class JsonRoomStore : IRoomStore
{
    private const int CurrentSchemaVersion = 1;

    private readonly string _path;
    private readonly ILogger<JsonRoomStore> _logger;

    public JsonRoomStore(JsonStoreOptions options, ILogger<JsonRoomStore> logger)
    {
        Directory.CreateDirectory(options.RootDirectory);
        _path = Path.Combine(options.RootDirectory, "rooms.json");
        _logger = logger;
    }

    public async Task<IReadOnlyList<Room>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path))
            return Array.Empty<Room>();

        try
        {
            await using var stream = File.OpenRead(_path);
            var document = await JsonSerializer
                .DeserializeAsync<RoomsDocument>(stream, RoomsJson.Options, ct)
                .ConfigureAwait(false);

            if (document is null)
                return Array.Empty<Room>();

            Migrate(document);
            return document.Rooms;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "rooms.json is unreadable; starting with no rooms.");
            return Array.Empty<Room>();
        }
    }

    public Task SaveAsync(IReadOnlyList<Room> rooms, CancellationToken ct = default)
    {
        var document = new RoomsDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Rooms = rooms.ToList(),
        };

        return AtomicFile.WriteAsync(
            _path,
            stream => JsonSerializer.SerializeAsync(stream, document, RoomsJson.Options, ct),
            ct);
    }

    private static void Migrate(RoomsDocument document)
    {
        // Future schema migrations branch on document.SchemaVersion here.
        if (document.SchemaVersion < CurrentSchemaVersion)
            document.SchemaVersion = CurrentSchemaVersion;
    }

    private sealed class RoomsDocument
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<Room> Rooms { get; set; } = new();
    }
}
