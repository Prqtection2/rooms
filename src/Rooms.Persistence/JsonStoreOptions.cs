namespace Rooms.Persistence;

/// <summary>
/// Where the JSON stores keep their files. Injected so tests can point at a temp
/// directory and production can point at %AppData%\Rooms.
/// </summary>
public sealed class JsonStoreOptions
{
    public required string RootDirectory { get; init; }
}
