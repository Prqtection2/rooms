using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Rooms.Application.Rooms;
using Rooms.Application.Rules;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.App.ViewModels;

/// <summary>A currently-open window the user can capture into a room (§12.3).</summary>
public sealed record RunningWindow(string ProcessName, string? ExecutablePath, string Display);

/// <summary>Create or edit a room (§12.3): name, accent, owned-process matchers, auto-launch apps,
/// plus "capture from open windows" helpers that snapshot what's running right now.</summary>
public partial class RoomEditorViewModel : ObservableObject
{
    private readonly IRoomManager _rooms;
    private readonly Room? _existing;

    public RoomEditorViewModel(IRoomManager rooms, IWindowService windows, Room? existing = null)
    {
        _rooms = rooms;
        _existing = existing;

        if (existing is not null)
        {
            Name = existing.Name;
            IsHome = existing.IsCatchAll;
            AccentColorHex = existing.AccentColorHex ?? string.Empty;
            foreach (var matcher in existing.OwnedWindowMatchers)
                if (!string.IsNullOrWhiteSpace(matcher.ProcessName))
                    MatcherProcessNames.Add(matcher.ProcessName!);
            foreach (var app in existing.AutoLaunchApps)
            {
                if (app.IsolatedInstance)
                    IsolatedBrowsers.Add(app.ExecutablePath);
                else
                    AutoLaunchPaths.Add(app.ExecutablePath);
            }
        }

        PopulateRunningWindows(windows);
    }

    public string Title => _existing is null ? "New room" : "Edit room";

    public bool CanDelete => _existing is not null;

    [ObservableProperty]
    private string _name = "New Room";

    /// <summary>When true this is a catch-all "Home" room: it keeps every window visible and
    /// ignores the owned-app matchers (§6.1). A safe room you can always return to.</summary>
    [ObservableProperty]
    private bool _isHome;

    [ObservableProperty]
    private string _accentColorHex = "#3B82F6";

    [ObservableProperty]
    private string _newMatcherProcess = string.Empty;

    [ObservableProperty]
    private string _newAutoLaunchPath = string.Empty;

    public ObservableCollection<string> MatcherProcessNames { get; } = new();

    public ObservableCollection<string> AutoLaunchPaths { get; } = new();

    /// <summary>Snapshot of currently-open windows the user can capture into this room.</summary>
    public ObservableCollection<RunningWindow> RunningWindows { get; } = new();

    /// <summary>Browsers that open as a separate, room-owned instance (their own profile).</summary>
    public ObservableCollection<string> IsolatedBrowsers { get; } = new();

    public IReadOnlyList<string> AvailableBrowsers { get; } = new[] { "msedge", "chrome", "firefox", "brave" };

    [ObservableProperty]
    private string _newIsolatedBrowser = "msedge";

    public event EventHandler? Saved;

    [RelayCommand]
    private void AddMatcher() => AddProcess(NewMatcherProcess, () => NewMatcherProcess = string.Empty);

    [RelayCommand]
    private void RemoveMatcher(string? value)
    {
        if (value is not null)
            MatcherProcessNames.Remove(value);
    }

    /// <summary>Add one running app as a default: recognise its windows (matcher) and auto-launch
    /// it on entry/reset (§12.3 "pick a window").</summary>
    [RelayCommand]
    private void AddRunning(RunningWindow? window)
    {
        if (window is not null)
            AddDefaultFrom(window);
    }

    /// <summary>Make every open app a default for this room (§12.3 "capture current layout").</summary>
    [RelayCommand]
    private void CaptureOpenApps()
    {
        foreach (var window in RunningWindows)
            AddDefaultFrom(window);
    }

    private void AddDefaultFrom(RunningWindow window)
    {
        AddProcess(window.ProcessName);
        if (!string.IsNullOrWhiteSpace(window.ExecutablePath) &&
            !AutoLaunchPaths.Contains(window.ExecutablePath, StringComparer.OrdinalIgnoreCase))
        {
            AutoLaunchPaths.Add(window.ExecutablePath);
        }
    }

    [RelayCommand]
    private void BrowseExe()
    {
        var dialog = new OpenFileDialog { Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*" };
        if (dialog.ShowDialog() == true)
            NewAutoLaunchPath = dialog.FileName;
    }

    [RelayCommand]
    private void AddAutoLaunch()
    {
        var value = NewAutoLaunchPath.Trim();
        if (value.Length > 0 && !AutoLaunchPaths.Contains(value, StringComparer.OrdinalIgnoreCase))
            AutoLaunchPaths.Add(value);

        NewAutoLaunchPath = string.Empty;
    }

    [RelayCommand]
    private void RemoveAutoLaunch(string? value)
    {
        if (value is not null)
            AutoLaunchPaths.Remove(value);
    }

    [RelayCommand]
    private void AddIsolatedBrowser()
    {
        var value = NewIsolatedBrowser?.Trim();
        if (!string.IsNullOrEmpty(value) && !IsolatedBrowsers.Contains(value, StringComparer.OrdinalIgnoreCase))
            IsolatedBrowsers.Add(value);
    }

    [RelayCommand]
    private void RemoveIsolatedBrowser(string? value)
    {
        if (value is not null)
            IsolatedBrowsers.Remove(value);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_existing is null)
            return;

        await _rooms.DeleteAsync(_existing.Id);
        Saved?.Invoke(this, EventArgs.Empty); // closes the editor + refreshes the switcher
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return;

        var room = _existing ?? new Room { Id = Guid.NewGuid(), OrderIndex = _rooms.Rooms.Count };
        room.Name = Name.Trim();
        room.IsCatchAll = IsHome;
        room.AccentColorHex = string.IsNullOrWhiteSpace(AccentColorHex) ? null : AccentColorHex.Trim();
        room.OwnedWindowMatchers = MatcherProcessNames
            .Select(p => new WindowMatcher { ProcessName = p })
            .ToList();
        room.AutoLaunchApps = AutoLaunchPaths
            .Select(p => new AppLaunchSpec { ExecutablePath = p, LaunchOnEnter = true })
            .Concat(IsolatedBrowsers.Select(b => new AppLaunchSpec
            {
                ExecutablePath = b,
                LaunchOnEnter = true,
                IsolatedInstance = true,
            }))
            .ToList();

        await _rooms.UpdateAsync(room);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void AddProcess(string? process, Action? onAdded = null)
    {
        var value = process?.Trim();
        if (!string.IsNullOrEmpty(value) && !MatcherProcessNames.Contains(value, StringComparer.OrdinalIgnoreCase))
            MatcherProcessNames.Add(value);

        onAdded?.Invoke();
    }

    private void PopulateRunningWindows(IWindowService windows)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var window in windows.EnumerateTopLevelWindows())
        {
            // Only apps with a taskbar presence (on screen or minimised) - i.e. the apps open in
            // the active room - not the SW_HIDE-hidden windows belonging to other rooms.
            if (!window.HasTaskbarPresence)
                continue;

            if (string.IsNullOrWhiteSpace(window.ProcessName) || SafeProcessList.Contains(window.ProcessName))
                continue;

            if (seen.Add(window.ProcessName))
            {
                var title = string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title;
                RunningWindows.Add(new RunningWindow(
                    window.ProcessName, window.ExecutablePath, $"{window.ProcessName}  ·  {title}"));
            }
        }
    }
}
