namespace Rooms.Core.Models;

/// <summary>
/// Immutable snapshot of a top-level window as seen by the OS layer. (§4.7)
/// <see cref="Handle"/> is an opaque HWND; only the OS layer should dereference it.
/// </summary>
public sealed record WindowInfo(
    IntPtr Handle,
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    string Title,
    bool IsVisible,
    bool IsMinimized = false)
{
    /// <summary>True if the window currently has a taskbar button - i.e. it's on screen OR merely
    /// minimised (both keep a taskbar presence); false only when we've SW_HIDE-hidden it.</summary>
    public bool HasTaskbarPresence => IsVisible || IsMinimized;
}
