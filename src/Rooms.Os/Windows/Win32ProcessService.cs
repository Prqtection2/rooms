using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Rooms.Os.Windows;

/// <summary>
/// Process launching, inspection, and termination (§5.2). Launch/Kill use System.Diagnostics;
/// window lookup reuses <see cref="IWindowService"/>; graceful close posts WM_CLOSE.
/// </summary>
public sealed class Win32ProcessService : IProcessService
{
    private const uint WM_CLOSE = 0x0010;

    private readonly IWindowService _windows;
    private readonly ILogger<Win32ProcessService> _logger;

    public Win32ProcessService(IWindowService windows, ILogger<Win32ProcessService> logger)
    {
        _windows = windows;
        _logger = logger;
    }

    // TODO (process-watcher milestone): raise from an optional WMI/ETW watcher.
#pragma warning disable CS0067 // event is declared per §5.2 but not yet raised
    public event EventHandler<ProcessInfo>? ProcessStarted;
#pragma warning restore CS0067

    public int Launch(AppLaunchSpec spec) =>
        Start(spec.ExecutablePath, ResolveArguments(spec), spec.WorkingDirectory);

    public int LaunchIsolated(AppLaunchSpec spec, string isolationKey) =>
        Start(spec.ExecutablePath, ResolveIsolatedArguments(spec, isolationKey), spec.WorkingDirectory);

    private int Start(string executablePath, string arguments, string? workingDirectory)
    {
        try
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = true,
            };

            return Process.Start(startInfo)?.Id ?? 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            _logger.LogWarning(ex, "Failed to launch {Path}.", executablePath);
            return 0;
        }
    }

    public IReadOnlyList<WindowInfo> GetWindowsForProcess(int pid) =>
        _windows.EnumerateTopLevelWindows().Where(w => w.ProcessId == pid).ToList();

    public void TryCloseGracefully(int pid)
    {
        // WM_CLOSE lets the app prompt to save, unlike Kill.
        foreach (var window in GetWindowsForProcess(pid))
            PInvoke.PostMessage(new HWND(window.Handle), WM_CLOSE, default, default);
    }

    public void Kill(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            _logger.LogWarning(ex, "Failed to kill process {Pid}.", pid);
        }
    }

    private static readonly string[] ChromiumBrowsers = { "chrome", "msedge", "brave", "vivaldi", "opera" };

    private static string ResolveArguments(AppLaunchSpec spec)
    {
        if (!string.IsNullOrWhiteSpace(spec.Arguments))
            return spec.Arguments;

        var name = BrowserName(spec.ExecutablePath);
        if (ChromiumBrowsers.Contains(name))
            return "--new-window";
        if (name == "firefox")
            return "-new-window";

        return string.Empty;
    }

    /// <summary>Per-room isolation: give the browser its own profile directory keyed by the room,
    /// so each room gets a genuinely separate browser instance (own window, tabs, session).</summary>
    private static string ResolveIsolatedArguments(AppLaunchSpec spec, string isolationKey)
    {
        var baseArgs = spec.Arguments ?? string.Empty;
        var name = BrowserName(spec.ExecutablePath);

        if (ChromiumBrowsers.Contains(name))
            return $"{baseArgs} --user-data-dir=\"{ProfileDirectory(name, isolationKey)}\" --new-window".Trim();
        if (name == "firefox")
            return $"{baseArgs} -no-remote -profile \"{ProfileDirectory(name, isolationKey)}\" -new-window".Trim();

        // Non-browser single-instance apps can't be isolated; best-effort launch.
        return baseArgs;
    }

    private static string BrowserName(string executablePath) =>
        System.IO.Path.GetFileNameWithoutExtension(executablePath)?.ToLowerInvariant() ?? string.Empty;

    private static string ProfileDirectory(string browser, string isolationKey)
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rooms", "profiles", browser, isolationKey);
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }
}
