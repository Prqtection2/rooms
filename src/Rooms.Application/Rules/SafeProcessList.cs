namespace Rooms.Application.Rules;

/// <summary>
/// Processes that an allowlist must never block (§8). An allowlist tries to block everything
/// not on it - including the shell and system UI - so the RuleEngine always unions the user's
/// allowlist with this constant safe-list to avoid fighting Explorer. Compared case-insensitively
/// against the process image name (no extension).
/// </summary>
public static class SafeProcessList
{
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Rooms itself.
        "Rooms.App",
        "Rooms",

        // The Windows shell and core UI surfaces (these own real windows).
        "explorer",
        "dwm",
        "ApplicationFrameHost",   // hosts UWP/store-app windows
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "SearchHost",
        "SearchApp",
        "TextInputHost",
        "LockApp",
        "SystemSettings",
        "sihost",
        "ctfmon",
        "taskmgr",                // never trap the user's escape hatch
    };

    public static bool Contains(string? processName) =>
        !string.IsNullOrEmpty(processName) && Names.Contains(processName);
}
