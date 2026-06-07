using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rooms.App.ViewModels;
using Rooms.App.Views;
using Rooms.Application;
using Rooms.Core.Abstractions;
using Rooms.Os;
using Rooms.Persistence;
using Serilog;

namespace Rooms.App.Infrastructure;

/// <summary>
/// Composition root. Builds the Generic Host that wires the application services to their
/// OS and persistence adapters, with Serilog as the logging backbone. This is the single
/// place that knows about every layer.
/// </summary>
public static class AppHost
{
    public static IHost Build()
    {
        Directory.CreateDirectory(AppPaths.Logs);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(AppPaths.Logs, "rooms-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true)
            .CreateLogger();

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(dispose: true);

        // Bottom-up: persistence + OS adapters, then the application services that use them.
        builder.Services.AddRoomsPersistence(AppPaths.Root);
        builder.Services.AddRoomsOs();
        builder.Services.AddRoomsApplication();

        // UI-owned services.
        builder.Services.AddSingleton<INotificationService, TrayNotificationService>();
        builder.Services.AddSingleton<TrayIconController>();

        // MVVM (§12): view-models + the reusable switcher overlay window.
        builder.Services.AddSingleton<FocusBarViewModel>();
        builder.Services.AddSingleton<SwitcherViewModel>();
        builder.Services.AddSingleton<SwitcherOverlayWindow>();
        builder.Services.AddTransient<SettingsViewModel>();

        return builder.Build();
    }
}
