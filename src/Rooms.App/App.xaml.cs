using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rooms.App.Infrastructure;
using Rooms.Application.Focus;
using Rooms.Application.Hotkeys;
using Rooms.Application.Rooms;
using Rooms.Application.Rules;
using Rooms.Application.Startup;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;
using Serilog;

namespace Rooms.App;

/// <summary>
/// Application entry point and lifetime owner. Builds and starts the host, loads rooms,
/// registers hotkeys, and brings up the tray icon. Tears everything down on exit.
/// </summary>
public partial class App : System.Windows.Application
{
    // Per-user, single-session name so only one Rooms runs at a time (§13).
    private const string SingleInstanceMutexName = @"Local\Rooms-9F3A2C71-5B6E-4D2A-9E2B-RoomsApp";

    private IHost? _host;
    private TrayIconController? _tray;
    private Mutex? _singleInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance enforcement (§13): bail out if Rooms is already running.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show(
                "Rooms is already running. Look for the blue \"R\" icon in the system tray (click the ^ arrow near the clock).",
                "Rooms",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        try
        {
            _host = AppHost.Build();
            await _host.StartAsync();

            var rooms = _host.Services.GetRequiredService<IRoomManager>();
            await rooms.InitializeAsync();

            // Begin reacting to window/process events for the active room (§7.5).
            _host.Services.GetRequiredService<IRuleEnforcer>().Start();
            // Opt-in focus re-assertion (§10).
            _host.Services.GetRequiredService<IFocusReassertionService>().Start();

            var hotkeys = _host.Services.GetRequiredService<IHotkeyService>();
            await hotkeys.InitializeAsync();

            _tray = _host.Services.GetRequiredService<TrayIconController>();
            _tray.Initialize();

            // §7.6: lost-window failsafe, run-at-login sync, restore default room.
            await _host.Services.GetRequiredService<IStartupService>().RunStartupAsync();

            Log.Information("Rooms started with {Count} room(s).", rooms.Rooms.Count);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed.");
            MessageBox.Show(
                $"Rooms failed to start:\n\n{ex.Message}",
                "Rooms",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();

        if (_host is not null)
        {
            // Clean-exit failsafe (§6): re-show everything we hid, then clear the journal so the
            // next launch doesn't try to "recover" windows that are already visible.
            try
            {
                _host.Services.GetRequiredService<IWindowService>().RestoreAllHidden();

                // Clear the failsafe hidden set on a clean exit (keep last-active for next launch).
                var rooms = _host.Services.GetRequiredService<IRoomManager>();
                _host.Services.GetRequiredService<IAppStateStore>().SaveAsync(new AppState
                {
                    LastActiveRoomId = rooms.ActiveRoom?.Id,
                    HiddenWindowHandles = new List<long>(),
                }).GetAwaiter().GetResult();

                // Restore the hosts file so blocked domains aren't left blocked after exit (§9).
                _host.Services.GetRequiredService<IWebsiteBlocker>().ClearAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Clean-exit restore failed.");
            }

            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        _singleInstanceMutex?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
