using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rooms.App.ViewModels;
using Rooms.App.Views;
using Rooms.Application.Focus;
using Rooms.Application.Hotkeys;
using Rooms.Application.Rooms;
using Rooms.Application.Switching;
using Rooms.Core.Abstractions;

namespace Rooms.App.Infrastructure;

/// <summary>
/// Owns the tray icon and all UI surfaces (§12): the left-click popup switcher, the right-click
/// menu, the hotkey-driven overlay, and the room-editor / settings windows. Bridges hotkeys and
/// VM events to window operations.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly INotificationService _notifications;
    private readonly IHotkeyService _hotkeys;
    private readonly IFocusSessionService _focus;
    private readonly IRoomManager _rooms;
    private readonly ILogger<TrayIconController> _logger;

    private TaskbarIcon? _tray;
    private SwitcherViewModel? _switcher;

    public TrayIconController(
        IServiceProvider services,
        INotificationService notifications,
        IHotkeyService hotkeys,
        IFocusSessionService focus,
        IRoomManager rooms,
        ILogger<TrayIconController> logger)
    {
        _services = services;
        _notifications = notifications;
        _hotkeys = hotkeys;
        _focus = focus;
        _rooms = rooms;
        _logger = logger;
    }

    private static System.Windows.Threading.Dispatcher Dispatcher => System.Windows.Application.Current.Dispatcher;

    public void Initialize()
    {
        _switcher = _services.GetRequiredService<SwitcherViewModel>();
        _switcher.NewRoomRequested += (_, _) => OpenRoomEditor(existingRoomId: null);
        _switcher.EditRoomRequested += (_, roomId) => OpenRoomEditor(roomId);
        _switcher.SettingsRequested += (_, _) => OpenSettings();
        _switcher.FocusSetupRequested += (_, _) => OpenFocusDialog();
        _switcher.CloseRequested += (_, _) => ClosePopup();

        _tray = new TaskbarIcon
        {
            ToolTipText = "Rooms — left-click to switch, double-click for the overlay",
            Icon = TrayIconFactory.CreateRoomsIcon(),
            ContextMenu = BuildContextMenu(),
            TrayPopup = new RoomSwitcherView { DataContext = _switcher },
        };
        _tray.TrayMouseDoubleClick += (_, _) => ShowOverlay();
        _tray.ForceCreate(enablesEfficiencyMode: false);

        if (_notifications is TrayNotificationService trayNotifications)
            trayNotifications.AttachToastHandler(ShowBalloon);

        _hotkeys.SwitcherHotkeyPressed += (_, _) => Dispatcher.Invoke(ShowOverlay);
        _hotkeys.ToggleFocusPressed += (_, _) => Dispatcher.Invoke(ToggleFocus);

        _logger.LogInformation("Tray icon initialised.");
        _notifications.ShowToast("Rooms", "Running in the system tray.");

        ShowFirstRunIfNeeded();
    }

    /// <summary>First-run experience (M8): with no rooms yet, welcome the user and open the editor.</summary>
    private void ShowFirstRunIfNeeded()
    {
        if (_rooms.Rooms.Count > 0)
            return;

        _notifications.ShowToast("Welcome to Rooms", "Let's create your first room.");
        // Defer until startup finishes so the modal editor doesn't block initialisation.
        Dispatcher.BeginInvoke(new Action(() => OpenRoomEditor(existingRoomId: null)));
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var switcher = new MenuItem { Header = "Open switcher" };
        switcher.Click += (_, _) => ShowOverlay();

        var startFocus = new MenuItem { Header = "Start focus session…" };
        startFocus.Click += (_, _) => OpenFocusDialog();

        var stopFocus = new MenuItem { Header = "Stop focus session" };
        stopFocus.Click += (_, _) => StopFocus();

        var reset = new MenuItem { Header = "Reset current room" };
        reset.Click += (_, _) => ResetCurrentRoom();

        var showAll = new MenuItem { Header = "Show all windows" };
        showAll.Click += (_, _) => ShowAll();

        var settings = new MenuItem { Header = "Settings…" };
        settings.Click += (_, _) => OpenSettings();

        var debug = new MenuItem { Header = "Debug: window ownership…" };
        debug.Click += (_, _) => OpenDebug();

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        menu.Items.Add(switcher);
        menu.Items.Add(startFocus);
        menu.Items.Add(stopFocus);
        menu.Items.Add(new Separator());
        menu.Items.Add(reset);
        menu.Items.Add(showAll);
        menu.Items.Add(new Separator());
        menu.Items.Add(settings);
        menu.Items.Add(debug);
        menu.Items.Add(exit);
        return menu;
    }

    private void ShowOverlay() => _services.GetRequiredService<SwitcherOverlayWindow>().ShowOverlay();

    /// <summary>Hotkey toggle: end an active session, or open the "start focus" dialog.</summary>
    private void ToggleFocus()
    {
        if (_focus.Current is not null)
            StopFocus();
        else
            OpenFocusDialog();
    }

    private void OpenFocusDialog()
    {
        if (_focus.Current is not null)
        {
            _notifications.ShowToast("Focus", "A session is already running. Open the switcher (Ctrl+Alt+R) to end it.");
            return;
        }

        _services.GetRequiredService<SwitcherOverlayWindow>().Hide(); // get the overlay out of the way
        var orchestrator = _services.GetRequiredService<ISwitchOrchestrator>();
        var viewModel = new FocusSessionDialogViewModel(_focus, orchestrator, _rooms);
        new FocusSessionDialog(viewModel).ShowDialog();
    }

    private void StopFocus()
    {
        if (_focus.Current is null)
        {
            _notifications.ShowToast("Focus", "No focus session is running.");
            return;
        }

        _focus.Stop();
        _notifications.ShowToast("Focus ended", "Focus session stopped.");
    }

    private async void ResetCurrentRoom()
    {
        var active = _rooms.ActiveRoom;
        if (active is null)
        {
            _notifications.ShowToast("Reset", "No room is active.");
            return;
        }

        await _services.GetRequiredService<ISwitchOrchestrator>().ResetRoomAsync(active.Id);
        _notifications.ShowToast("Room reset", $"\"{active.Name}\" is back to its defaults.");
    }

    private async void ShowAll()
    {
        await _services.GetRequiredService<ISwitchOrchestrator>().ShowAllAsync();
        _notifications.ShowToast("Rooms", "Showing all windows; left all rooms.");
    }

    private void OpenRoomEditor(Guid? existingRoomId)
    {
        ClosePopup();
        var existing = existingRoomId is Guid id ? _rooms.Get(id) : null;
        var windows = _services.GetRequiredService<IWindowService>();
        var viewModel = new RoomEditorViewModel(_rooms, windows, existing);
        var window = new RoomEditorWindow(viewModel);

        if (window.ShowDialog() == true)
            _switcher?.Refresh();
    }

    private async void OpenSettings()
    {
        ClosePopup();
        var viewModel = _services.GetRequiredService<SettingsViewModel>();
        await viewModel.InitializeAsync();
        new SettingsWindow(viewModel).ShowDialog();
        _switcher?.Refresh(); // pick up rooms created by "Set up testing rooms"
    }

    /// <summary>Open the (non-modal) diagnostic listing of every window and its room ownership.</summary>
    private void OpenDebug()
    {
        ClosePopup();
        var viewModel = new WindowDebugViewModel(
            _services.GetRequiredService<IWindowService>(),
            _services.GetRequiredService<Rooms.Application.Windows.IWindowRegistry>(),
            _services.GetRequiredService<Rooms.Application.Rules.IRuleEngine>(),
            _rooms);
        new WindowDebugWindow(viewModel) { Owner = null }.Show();
    }

    private void ClosePopup() => _tray?.CloseTrayPopup();

    private void ShowBalloon(string title, string message) =>
        _tray?.ShowNotification(title, message, NotificationIcon.Info);

    public void Dispose() => _tray?.Dispose();
}
