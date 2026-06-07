using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rooms.Application.Focus;
using Rooms.Core.Models;

namespace Rooms.App.ViewModels;

/// <summary>Focus-session countdown widget with a surrenderable "Give up" affordance (§12.5 / §10).</summary>
public partial class FocusBarViewModel : ObservableObject, IDisposable
{
    private readonly IFocusSessionService _focus;

    public FocusBarViewModel(IFocusSessionService focus)
    {
        _focus = focus;
        _focus.SessionStarted += OnSessionStarted;
        _focus.SessionEnded += OnSessionEnded;
        _focus.Tick += OnTick;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInactive))]
    private bool _isActive;

    [ObservableProperty]
    private string _remaining = string.Empty;

    /// <summary>Convenience inverse of <see cref="IsActive"/> for "Start focus" visibility.</summary>
    public bool IsInactive => !IsActive;

    private void OnSessionStarted(object? sender, FocusSession session) =>
        UiThread.Post(() =>
        {
            IsActive = true;
            Remaining = Format(session.Duration);
        });

    private void OnSessionEnded(object? sender, FocusSession session) =>
        UiThread.Post(() =>
        {
            IsActive = false;
            Remaining = string.Empty;
        });

    private void OnTick(object? sender, TimeSpan remaining) =>
        UiThread.Post(() => Remaining = Format(remaining));

    [RelayCommand]
    private void GiveUp() => _focus.Stop();

    internal static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    public void Dispose()
    {
        _focus.SessionStarted -= OnSessionStarted;
        _focus.SessionEnded -= OnSessionEnded;
        _focus.Tick -= OnTick;
    }
}
