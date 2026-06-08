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

    /// <summary>
    /// System UI that must never be HIDDEN on a room switch (it stays visible in every room).
    /// This is <see cref="Names"/> minus "explorer": a File Explorer window is a normal app window
    /// that should be roomed like any other (hidden when you leave its room). The shell's own
    /// desktop and taskbar - also explorer.exe - are filtered out earlier by window class, so they
    /// are never hidden regardless. "explorer" stays in <see cref="Names"/> so rules can never
    /// close/kill the shell process.
    /// </summary>
    public static readonly IReadOnlySet<string> NeverHideNames = new HashSet<string>(
        Names.Where(n => !string.Equals(n, "explorer", StringComparison.OrdinalIgnoreCase)),
        StringComparer.OrdinalIgnoreCase);

    public static bool Contains(string? processName) =>
        !string.IsNullOrEmpty(processName) && Names.Contains(processName);

    /// <summary>True if this process's windows should never be hidden on a room switch.</summary>
    public static bool IsNeverHidden(string? processName) =>
        !string.IsNullOrEmpty(processName) && NeverHideNames.Contains(processName);
}
