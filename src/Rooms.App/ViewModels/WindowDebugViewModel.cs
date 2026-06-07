using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rooms.Application.Rooms;
using Rooms.Application.Rules;
using Rooms.Application.Windows;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.App.ViewModels;

/// <summary>One enumerated top-level window and where Rooms thinks it belongs.</summary>
public sealed record WindowDebugRow(string Process, string Title, string Visible, string Owner);

/// <summary>
/// Diagnostic view: lists every top-level window with its process, title, visibility and which
/// room owns it (assigned vs. rule-matched vs. loose). Lets you see at a glance why a window
/// appears in a room's taskbar or not.
/// </summary>
public partial class WindowDebugViewModel : ObservableObject
{
    private readonly IWindowService _windows;
    private readonly IWindowRegistry _registry;
    private readonly IRuleEngine _rules;
    private readonly IRoomManager _rooms;

    public WindowDebugViewModel(IWindowService windows, IWindowRegistry registry, IRuleEngine rules, IRoomManager rooms)
    {
        _windows = windows;
        _registry = registry;
        _rules = rules;
        _rooms = rooms;
        Refresh();
    }

    public ObservableCollection<WindowDebugRow> Rows { get; } = new();

    [ObservableProperty]
    private string _header = "";

    [RelayCommand]
    public void Refresh()
    {
        Rows.Clear();

        var active = _rooms.ActiveRoom;
        var visibleCount = 0;

        foreach (var window in _windows.EnumerateTopLevelWindows()
                     .OrderBy(w => w.ProcessName)
                     .ThenBy(w => w.Title))
        {
            if (window.HasTaskbarPresence)
                visibleCount++;

            Rows.Add(new WindowDebugRow(
                window.ProcessName,
                Truncate(window.Title, 48),
                window.IsVisible ? "yes" : window.IsMinimized ? "min" : "—",
                OwnerOf(window)));
        }

        Header = $"Active room: {active?.Name ?? "(none — showing all)"}   ·   " +
                 $"{Rows.Count} windows ({visibleCount} on taskbar)";
    }

    /// <summary>Resolve where this window belongs, mirroring the switch logic's ownership model.</summary>
    private string OwnerOf(WindowInfo window)
    {
        var explicitRoom = _registry.GetExplicitRoom(window);
        if (explicitRoom is Guid id)
        {
            var name = _rooms.Get(id)?.Name ?? "?";
            return $"{name}  ({Describe(_registry.GetSource(window))})";
        }

        var ruleRoom = _rooms.Rooms.FirstOrDefault(r => !r.IsCatchAll && _rules.IsOwnedBy(window, r));
        if (ruleRoom is not null)
            return $"{ruleRoom.Name}  (rule)";

        return "loose / unassigned";
    }

    private static string Describe(AssignmentSource source) => source switch
    {
        AssignmentSource.ManualOverride => "assigned",
        AssignmentSource.LaunchedByRoom => "launched here",
        AssignmentSource.MatchedByRule => "adopted",
        _ => "—",
    };

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) ? "(no title)" : value.Length <= max ? value : value[..max] + "…";
}
