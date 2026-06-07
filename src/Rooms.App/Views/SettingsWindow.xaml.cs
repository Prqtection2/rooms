using System.Windows;
using System.Windows.Input;
using Rooms.App.ViewModels;
using CoreModifierKeys = Rooms.Core.Models.ModifierKeys;

namespace Rooms.App.Views;

/// <summary>Settings (§12.4), including live hotkey capture handled here in the code-behind.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.Saved += (_, _) => Close();
        CloseButton.Click += (_, _) => Close();
        Closed += OnClosed;
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        // Re-arm the live hotkeys (they were suspended while the dialog was open).
        await _viewModel.RestoreHotkeysAsync();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_viewModel.CapturingRow is null)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (IsModifierKey(key))
        {
            e.Handled = true; // wait for the non-modifier key
            return;
        }

        if (key == Key.Escape)
        {
            _viewModel.CancelCapture();
            e.Handled = true;
            return;
        }

        var modifiers = MapModifiers(Keyboard.Modifiers);
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        _viewModel.ApplyCapture(modifiers, virtualKey);
        e.Handled = true;
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System;

    private static CoreModifierKeys MapModifiers(ModifierKeys wpf)
    {
        var result = CoreModifierKeys.None;
        if (wpf.HasFlag(ModifierKeys.Alt)) result |= CoreModifierKeys.Alt;
        if (wpf.HasFlag(ModifierKeys.Control)) result |= CoreModifierKeys.Control;
        if (wpf.HasFlag(ModifierKeys.Shift)) result |= CoreModifierKeys.Shift;
        if (wpf.HasFlag(ModifierKeys.Windows)) result |= CoreModifierKeys.Windows;
        return result;
    }
}
