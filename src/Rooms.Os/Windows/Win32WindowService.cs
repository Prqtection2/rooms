using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Rooms.Os.Windows;

/// <summary>
/// Win32 implementation of <see cref="IWindowService"/> (§5.1). Hiding a window uses SW_HIDE so it
/// disappears completely from the taskbar, Alt-Tab, and the screen - that's what makes a room feel
/// like a room (you only see the current room's apps). Opening a new app in a room creates a fresh
/// visible window, which lands in that room's taskbar and is adopted into the room.
///
/// Crucially, we remember each window's show-state (minimised / maximised / normal) at hide time and
/// restore that exact state - a minimised window comes back minimised, a maximised one comes back
/// maximised - instead of yanking everything open. Restores avoid stealing focus (SW_SHOW*NOACTIVE)
/// so returning to a room doesn't reshuffle the foreground.
/// </summary>
public sealed class Win32WindowService : IWindowService, IDisposable
{
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_APPWINDOW = 0x00040000;
    private const uint WM_QUIT = 0x0012;
    private const int OBJID_WINDOW = 0;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;

    // The shell's own structural windows. They run inside explorer.exe and otherwise look like
    // ordinary top-level windows, so we exclude them by class - a room switch must never hide the
    // desktop or the taskbar. (A real File Explorer window, class CabinetWClass, is NOT here, so it
    // is treated as a normal, roomable app window.)
    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.Ordinal)
    {
        "Progman",                // desktop ("Program Manager")
        "WorkerW",                // desktop wallpaper host
        "Shell_TrayWnd",          // primary taskbar
        "Shell_SecondaryTrayWnd", // taskbar on additional monitors
    };

    private readonly ILogger<Win32WindowService> _logger;
    // Handle -> the show-state the window had when we hid it, so Show() can restore it faithfully.
    private readonly Dictionary<IntPtr, WindowShowState> _hiddenByUs = new();
    private readonly object _hiddenLock = new();

    // WinEvent hook plumbing, started lazily on first subscription.
    private readonly object _hookLock = new();
    private Thread? _hookThread;
    private uint _hookThreadId;
    private WINEVENTPROC? _winEventProc; // rooted so the GC doesn't collect the callback
    private bool _disposed;

    private EventHandler<WindowInfo>? _windowCreated;
    private EventHandler<WindowInfo>? _windowActivated;
    private EventHandler<IntPtr>? _windowDestroyed;

    public Win32WindowService(ILogger<Win32WindowService> logger) => _logger = logger;

    public event EventHandler<WindowInfo>? WindowCreated
    {
        add { _windowCreated += value; EnsureHookStarted(); }
        remove => _windowCreated -= value;
    }

    public event EventHandler<WindowInfo>? WindowActivated
    {
        add { _windowActivated += value; EnsureHookStarted(); }
        remove => _windowActivated -= value;
    }

    public event EventHandler<IntPtr>? WindowDestroyed
    {
        add { _windowDestroyed += value; EnsureHookStarted(); }
        remove => _windowDestroyed -= value;
    }

    public IReadOnlyList<WindowInfo> EnumerateTopLevelWindows()
    {
        var results = new List<WindowInfo>();

        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (IsManageableWindow(hwnd))
                results.Add(Describe(hwnd));

            return true;
        }, default);

        return results;
    }

    public IReadOnlyCollection<IntPtr> HiddenWindows
    {
        get
        {
            lock (_hiddenLock)
                return _hiddenByUs.Keys.ToArray();
        }
    }

    public void Hide(IntPtr handle)
    {
        var hwnd = new HWND(handle);
        if (!PInvoke.IsWindow(hwnd)) // stale-handle guard (§6)
            return;

        // Remember how the window looked BEFORE hiding (minimised / maximised / normal) so we can
        // put it back exactly the same when the user returns to this room.
        var state = CaptureShowState(hwnd);

        // Clean hide: removes the window from the taskbar, Alt-Tab, and screen.
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_HIDE);
        lock (_hiddenLock)
            _hiddenByUs[handle] = state;
    }

    public void Show(IntPtr handle)
    {
        WindowShowState state;
        lock (_hiddenLock)
        {
            _hiddenByUs.TryGetValue(handle, out state); // Normal (default) if we didn't hide it
            _hiddenByUs.Remove(handle);
        }

        var hwnd = new HWND(handle);
        if (!PInvoke.IsWindow(hwnd))
            return;

        // Restore the window to the exact state it had when hidden, without stealing focus: a
        // minimised window goes back to the taskbar (not popped open), a maximised one returns
        // maximised, a normal one returns in place.
        var command = state switch
        {
            WindowShowState.Minimized => SHOW_WINDOW_CMD.SW_SHOWMINNOACTIVE,
            WindowShowState.Maximized => SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED,
            _ => SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE,
        };
        PInvoke.ShowWindow(hwnd, command);
    }

    private static WindowShowState CaptureShowState(HWND hwnd)
    {
        if (PInvoke.IsIconic(hwnd))
            return WindowShowState.Minimized;
        if (PInvoke.IsZoomed(hwnd))
            return WindowShowState.Maximized;
        return WindowShowState.Normal;
    }

    public void Minimize(IntPtr handle)
    {
        var hwnd = new HWND(handle);
        if (PInvoke.IsWindow(hwnd))
            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_MINIMIZE);
    }

    public void BringToForeground(IntPtr handle)
    {
        var hwnd = new HWND(handle);
        if (!PInvoke.IsWindow(hwnd))
            return;

        if (PInvoke.IsIconic(hwnd))
            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);

        PInvoke.SetForegroundWindow(hwnd);
        lock (_hiddenLock)
            _hiddenByUs.Remove(handle);
    }

    public void RestoreAllHidden()
    {
        IntPtr[] handles;
        lock (_hiddenLock)
            handles = _hiddenByUs.Keys.ToArray();

        foreach (var handle in handles)
            Show(handle);

        _logger.LogInformation("Restored {Count} hidden window(s).", handles.Length);
    }

    public void SetPlacement(IntPtr handle, WindowPlacement placement)
    {
        var hwnd = new HWND(handle);
        if (!PInvoke.IsWindow(hwnd))
            return;

        switch (placement.ShowState)
        {
            case WindowShowState.Maximized:
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_MAXIMIZE);
                break;

            case WindowShowState.Minimized:
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_MINIMIZE);
                break;

            default:
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
                PInvoke.SetWindowPos(
                    hwnd,
                    HWND.Null,
                    placement.X,
                    placement.Y,
                    placement.Width,
                    placement.Height,
                    SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
                break;
        }
    }

    public WindowPlacement GetPlacement(IntPtr handle)
    {
        var hwnd = new HWND(handle);
        var placement = new WindowPlacement();

        if (PInvoke.GetWindowRect(hwnd, out var rect))
        {
            placement.X = rect.left;
            placement.Y = rect.top;
            placement.Width = rect.right - rect.left;
            placement.Height = rect.bottom - rect.top;
        }

        if (PInvoke.IsZoomed(hwnd))
            placement.ShowState = WindowShowState.Maximized;
        else if (PInvoke.IsIconic(hwnd))
            placement.ShowState = WindowShowState.Minimized;

        return placement;
    }

    private bool IsManageableWindow(HWND hwnd)
    {
        if (!PInvoke.IsWindowVisible(hwnd))
        {
            lock (_hiddenLock)
            {
                if (!_hiddenByUs.ContainsKey((IntPtr)hwnd))
                    return false;
            }
        }

        if (PInvoke.GetWindowTextLength(hwnd) == 0)
            return false;

        var exStyle = (uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
            return false;

        var owner = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER);
        if (!owner.IsNull && (exStyle & WS_EX_APPWINDOW) == 0)
            return false;

        if (IsCloaked(hwnd))
            return false;

        // Never manage the desktop/taskbar (explorer.exe shell windows); File Explorer is fine.
        return !ShellWindowClasses.Contains(GetClassNameOf(hwnd));
    }

    private static string GetClassNameOf(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        var copied = PInvoke.GetClassName(hwnd, buffer);
        return copied > 0 ? new string(buffer[..copied]) : string.Empty;
    }

    private static unsafe bool IsCloaked(HWND hwnd)
    {
        var cloaked = 0;
        var hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, (uint)sizeof(int));
        return hr.Succeeded && cloaked != 0;
    }

    private WindowInfo Describe(HWND hwnd)
    {
        var pid = GetProcessId(hwnd);

        // "On screen" vs "merely minimised" are tracked separately: a minimised window is NOT on
        // screen, but it still owns a taskbar button - so the switch engine must still hide it when
        // it doesn't belong to the active room. (A SW_HIDE-hidden window is neither.)
        var onScreen = PInvoke.IsWindowVisible(hwnd);
        var iconic = PInvoke.IsIconic(hwnd);

        return new WindowInfo(
            (IntPtr)hwnd,
            (int)pid,
            ResolveProcessName(pid),
            ResolveExecutablePath(pid),
            GetTitle(hwnd),
            IsVisible: onScreen && !iconic,
            IsMinimized: onScreen && iconic);
    }

    private static string GetTitle(HWND hwnd)
    {
        var length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0)
            return string.Empty;

        Span<char> buffer = length < 256 ? stackalloc char[length + 1] : new char[length + 1];
        var copied = PInvoke.GetWindowText(hwnd, buffer);
        return new string(buffer[..copied]);
    }

    private static uint GetProcessId(HWND hwnd)
    {
        PInvoke.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    private string ResolveProcessName(uint pid)
    {
        if (pid == 0)
            return string.Empty;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _logger.LogTrace("Could not resolve process name for pid {Pid}.", pid);
            return string.Empty;
        }
    }

    private static unsafe string? ResolveExecutablePath(uint pid)
    {
        if (pid == 0)
            return null;

        using var handle = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle.IsInvalid)
            return null;

        Span<char> buffer = stackalloc char[260];
        uint size = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(handle, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size)
            ? new string(buffer[..(int)size])
            : null;
    }

    private void EnsureHookStarted()
    {
        lock (_hookLock)
        {
            if (_hookThread is not null || _disposed)
                return;

            _hookThread = new Thread(RunHookLoop)
            {
                IsBackground = true,
                Name = "Rooms.WinEvents",
            };
            _hookThread.Start();
        }
    }

    private void RunHookLoop()
    {
        _winEventProc = OnWinEvent;
        _hookThreadId = PInvoke.GetCurrentThreadId();

        // Hook 1: object create / destroy / show. Hook 2: foreground + restore-from-minimised,
        // so we notice when the user brings a window back (relaunch / Alt-Tab).
        var objectHook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_OBJECT_CREATE, PInvoke.EVENT_OBJECT_SHOW,
            HMODULE.Null, _winEventProc, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
        var activationHook = PInvoke.SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_MINIMIZEEND,
            HMODULE.Null, _winEventProc, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);

        if (objectHook.IsNull)
            _logger.LogWarning("SetWinEventHook (object) failed; window events are limited.");

        while (PInvoke.GetMessage(out _, default, 0, 0)) { }

        if (!objectHook.IsNull) PInvoke.UnhookWinEvent(objectHook);
        if (!activationHook.IsNull) PInvoke.UnhookWinEvent(activationHook);
    }

    private void OnWinEvent(
        HWINEVENTHOOK hook, uint eventId, HWND hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
    {
        if (idObject != OBJID_WINDOW || idChild != 0 || hwnd.IsNull)
            return;

        if (eventId == PInvoke.EVENT_OBJECT_DESTROY)
        {
            lock (_hiddenLock)
                _hiddenByUs.Remove((IntPtr)hwnd);
            _windowDestroyed?.Invoke(this, (IntPtr)hwnd);
            return;
        }

        if (eventId is EVENT_SYSTEM_FOREGROUND or EVENT_SYSTEM_MINIMIZEEND)
        {
            if (_windowActivated is not null && IsManageableWindow(hwnd))
                _windowActivated.Invoke(this, Describe(hwnd));
            return;
        }

        // EVENT_OBJECT_CREATE / EVENT_OBJECT_SHOW
        if (_windowCreated is not null && IsManageableWindow(hwnd))
            _windowCreated.Invoke(this, Describe(hwnd));
    }

    public void Dispose()
    {
        _disposed = true;

        lock (_hookLock)
        {
            if (_hookThread is not null && _hookThreadId != 0)
            {
                PInvoke.PostThreadMessage(_hookThreadId, WM_QUIT, default, default);
                _hookThread.Join(TimeSpan.FromSeconds(2));
                _hookThread = null;
            }
        }
    }
}
