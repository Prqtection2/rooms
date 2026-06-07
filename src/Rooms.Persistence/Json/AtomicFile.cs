namespace Rooms.Persistence.Json;

/// <summary>
/// Helpers for crash-safe file writes: serialise to a temp file, then atomically
/// replace the target so a failed write never leaves a half-written config.
/// </summary>
internal static class AtomicFile
{
    public static async Task WriteAsync(string path, Func<Stream, Task> writeBody, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = path + ".tmp";

        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await writeBody(stream).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        // File.Move with overwrite is the closest to atomic replace available cross-volume-safely.
        File.Move(temp, path, overwrite: true);
    }
}
