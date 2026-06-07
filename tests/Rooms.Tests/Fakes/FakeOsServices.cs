using Rooms.Application.Rules;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Tests.Fakes;

/// <summary>Records window operations so tests can assert what the orchestrator did.</summary>
public sealed class FakeWindowService : IWindowService
{
    private readonly List<WindowInfo> _windows;
    private readonly HashSet<IntPtr> _hidden = new();
    private readonly Dictionary<IntPtr, bool> _visible;

    public FakeWindowService(IEnumerable<WindowInfo>? windows = null)
    {
        _windows = windows?.ToList() ?? new List<WindowInfo>();
        _visible = _windows.ToDictionary(w => w.Handle, w => w.IsVisible);
    }

    public List<IntPtr> Shown { get; } = new();
    public List<IntPtr> Hidden { get; } = new();
    public List<IntPtr> Minimized { get; } = new();
    public List<IntPtr> Foregrounded { get; } = new();
    public bool RestoreAllHiddenCalled { get; private set; }

    // Reflect live visibility so the switch diff (toShow/toHide) is exercised realistically.
    public IReadOnlyList<WindowInfo> EnumerateTopLevelWindows() =>
        _windows.Select(w => w with { IsVisible = _visible[w.Handle] }).ToList();

    public IReadOnlyCollection<IntPtr> HiddenWindows => _hidden.ToArray();

    /// <summary>Handles of windows currently visible - the "visible set" property-tests compare.</summary>
    public IReadOnlyList<IntPtr> VisibleSet() => _visible.Where(kv => kv.Value).Select(kv => kv.Key).OrderBy(h => h).ToList();

    public void Hide(IntPtr handle)
    {
        Hidden.Add(handle);
        _hidden.Add(handle);
        _visible[handle] = false;
    }

    public void Show(IntPtr handle)
    {
        Shown.Add(handle);
        _hidden.Remove(handle);
        _visible[handle] = true;
    }

    public void Minimize(IntPtr handle) => Minimized.Add(handle); // minimized windows stay visible

    public void BringToForeground(IntPtr handle)
    {
        Foregrounded.Add(handle);
        _hidden.Remove(handle);
    }

    public void SetPlacement(IntPtr handle, WindowPlacement placement) { }
    public WindowPlacement GetPlacement(IntPtr handle) => new();

    public void RestoreAllHidden()
    {
        RestoreAllHiddenCalled = true;
        foreach (var handle in _hidden.ToArray())
            Show(handle);
    }

    public event EventHandler<WindowInfo>? WindowCreated;
    public event EventHandler<WindowInfo>? WindowActivated;

#pragma warning disable CS0067 // not raised in tests
    public event EventHandler<IntPtr>? WindowDestroyed;
#pragma warning restore CS0067

    public void RaiseWindowCreated(WindowInfo window) => WindowCreated?.Invoke(this, window);
    public void RaiseWindowActivated(WindowInfo window) => WindowActivated?.Invoke(this, window);
}

/// <summary>Records launch requests; never starts a real process.</summary>
public sealed class FakeProcessService : IProcessService
{
    public List<string> Launched { get; } = new();
    public List<int> ClosedGracefully { get; } = new();

    public List<(string Path, string Key)> LaunchedIsolated { get; } = new();

    public int Launch(AppLaunchSpec spec)
    {
        Launched.Add(spec.ExecutablePath);
        return 4242; // a non-zero fake pid
    }

    public int LaunchIsolated(AppLaunchSpec spec, string isolationKey)
    {
        Launched.Add(spec.ExecutablePath);
        LaunchedIsolated.Add((spec.ExecutablePath, isolationKey));
        return 4242;
    }

    public IReadOnlyList<WindowInfo> GetWindowsForProcess(int pid) => Array.Empty<WindowInfo>();
    public void TryCloseGracefully(int pid) => ClosedGracefully.Add(pid);
    public void Kill(int pid) { }

#pragma warning disable CS0067 // event unused in tests
    public event EventHandler<ProcessInfo>? ProcessStarted;
#pragma warning restore CS0067
}

/// <summary>No-op rule enforcer that records what it was told.</summary>
public sealed class FakeRuleEnforcer : IRuleEnforcer
{
    public bool Started { get; private set; }
    public Room? ActiveRoom { get; private set; }

    public void Start() => Started = true;
    public void SetActiveRoom(Room? room) => ActiveRoom = room;
}

/// <summary>Records wallpaper changes.</summary>
public sealed class FakeWallpaperService : IWallpaperService
{
    public List<string> Applied { get; } = new();
    public void SetWallpaper(string imagePath) => Applied.Add(imagePath);
}

/// <summary>Records toasts and the last Do-Not-Disturb state.</summary>
public sealed class FakeNotificationService : INotificationService
{
    public List<(string Title, string Message)> Toasts { get; } = new();
    public bool? DoNotDisturb { get; private set; }

    public void ShowToast(string title, string message) => Toasts.Add((title, message));
    public void SetDoNotDisturb(bool enabled) => DoNotDisturb = enabled;
}

/// <summary>Records website-blocking calls.</summary>
public sealed class FakeWebsiteBlocker : IWebsiteBlocker
{
    public bool IsAvailable { get; set; } = true;
    public List<IReadOnlyList<string>> Applied { get; } = new();
    public int ClearCount { get; private set; }

    public Task ApplyAsync(IReadOnlyList<string> blockedDomains)
    {
        Applied.Add(blockedDomains);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        ClearCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Shared helpers for building test <see cref="WindowInfo"/> values.</summary>
public static class TestWindows
{
    public static WindowInfo Make(
        IntPtr handle, string processName, string title = "", string? executablePath = null,
        bool isVisible = true, bool isMinimized = false) =>
        new(handle, (int)handle, processName, executablePath, title, isVisible, isMinimized);
}
