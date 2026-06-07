using System.IO;

namespace Rooms.App.Infrastructure;

/// <summary>Well-known on-disk locations for Rooms data and logs (under %AppData%\Rooms).</summary>
public static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rooms");

    public static string Logs { get; } = Path.Combine(Root, "logs");
}
