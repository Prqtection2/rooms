using System.Windows;
using System.Windows.Input;
using Rooms.App.ViewModels;

namespace Rooms.App.Views;

/// <summary>
/// Keyboard-driven, centered, always-on-top switcher overlay (§12.2). Arrow keys select,
/// number keys jump, Enter switches, Esc cancels. Closing on the active room keeps it simple
/// by hiding (the window is reused for each press of the open-switcher hotkey).
/// </summary>
public partial class SwitcherOverlayWindow : Window
{
    private readonly SwitcherViewModel _viewModel;

    public SwitcherOverlayWindow(SwitcherViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        _viewModel.CloseRequested += (_, _) => Hide();
    }

    /// <summary>Refresh, show, and focus the list with the active room pre-selected.</summary>
    public void ShowOverlay()
    {
        _viewModel.Refresh();

        Show();
        Activate();

        var activeIndex = _viewModel.ActiveRoom is null
            ? 0
            : _viewModel.Rooms.IndexOf(_viewModel.ActiveRoom);

        RoomList.SelectedIndex = _viewModel.Rooms.Count == 0 ? -1 : Math.Max(0, activeIndex);
        RoomList.Focus();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        Hide(); // dismiss when focus is lost, like Alt-Tab
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                return;

            case Key.Enter:
                SwitchSelected();
                e.Handled = true;
                return;
        }

        if (TryGetDigit(e.Key, out var digit) && digit >= 1 && digit <= _viewModel.Rooms.Count)
        {
            RoomList.SelectedIndex = digit - 1;
            SwitchSelected();
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }

    private void SwitchSelected()
    {
        if (RoomList.SelectedItem is RoomItemViewModel item && _viewModel.SwitchCommand.CanExecute(item))
            _viewModel.SwitchCommand.Execute(item);
    }

    private static bool TryGetDigit(Key key, out int digit)
    {
        if (key is >= Key.D1 and <= Key.D9)
        {
            digit = key - Key.D1 + 1;
            return true;
        }

        if (key is >= Key.NumPad1 and <= Key.NumPad9)
        {
            digit = key - Key.NumPad1 + 1;
            return true;
        }

        digit = 0;
        return false;
    }
}
