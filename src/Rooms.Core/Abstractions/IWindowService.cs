using Rooms.Core.Models;

namespace Rooms.Core.Abstractions;

/// <summary>
/// Top-level window manipulation. The single seam between the app and the Win32 windowing
/// surface; everything above the OS layer depends on this, not on user32. (§5.1)
/// </summary>
public interface IWindowService
{
    /// <summary>Real top-level app windows (visible or hidden-by-us), filtered of tool
    /// windows, owned windows, and cloaked windows.</summary>
    IReadOnlyList<WindowInfo> EnumerateTopLevelWindows();

    /// <summary>Hide a window (ShowWindow SW_HIDE) - removes it from taskbar and Alt-Tab.</summary>
    void Hide(IntPtr handle);

    /// <summary>Show / restore a window we previously hid or minimised.</summary>
    void Show(IntPtr handle);

    void Minimize(IntPtr handle);

    /// <summary>Move / resize a window (SetWindowPos) per a saved placement.</summary>
    void SetPlacement(IntPtr handle, WindowPlacement placement);

    WindowPlacement GetPlacement(IntPtr handle);

    void BringToForeground(IntPtr handle);

    /// <summary>Handles of windows currently hidden by this app (the lost-window failsafe set, §6).</summary>
    IReadOnlyCollection<IntPtr> HiddenWindows { get; }

    /// <summary>Show every window we hid. Called on clean shutdown so nothing is stranded.</summary>
    void RestoreAllHidden();

    /// <summary>Raised when a new top-level window appears (via a WinEvent hook).</summary>
    event EventHandler<WindowInfo>? WindowCreated;

    /// <summary>Raised when a window is brought to the foreground or restored from minimised
    /// (e.g. the user relaunched or Alt-Tabbed to it) - used to let it follow into the active room.</summary>
    event EventHandler<WindowInfo>? WindowActivated;

    /// <summary>Raised when a top-level window is destroyed.</summary>
    event EventHandler<IntPtr>? WindowDestroyed;
}
