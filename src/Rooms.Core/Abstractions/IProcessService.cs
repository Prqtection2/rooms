using Rooms.Core.Models;

namespace Rooms.Core.Abstractions;

/// <summary>Process launching, inspection, and termination. (§5.2)</summary>
public interface IProcessService
{
    /// <summary>Launch an app. Returns the new process id, or 0 if it could not be started.</summary>
    int Launch(AppLaunchSpec spec);

    /// <summary>Launch a per-room isolated instance (e.g. a browser with its own profile keyed by
    /// <paramref name="isolationKey"/>). Returns the new process id, or 0 on failure.</summary>
    int LaunchIsolated(AppLaunchSpec spec, string isolationKey);

    IReadOnlyList<WindowInfo> GetWindowsForProcess(int pid);

    /// <summary>Ask the process to close (WM_CLOSE to its main window) so unsaved work is
    /// not lost. Prefer this over <see cref="Kill"/>.</summary>
    void TryCloseGracefully(int pid);

    /// <summary>Forcibly terminate a process. Last resort.</summary>
    void Kill(int pid);

    /// <summary>Raised when a process starts (optional WMI watcher; may never fire).</summary>
    event EventHandler<ProcessInfo>? ProcessStarted;
}
