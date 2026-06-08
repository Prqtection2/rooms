using Microsoft.Extensions.DependencyInjection;
using Rooms.Application.Focus;
using Rooms.Application.Hotkeys;
using Rooms.Application.Rooms;
using Rooms.Application.Rules;
using Rooms.Application.Startup;
using Rooms.Application.Switching;
using Rooms.Application.Windows;

namespace Rooms.Application;

/// <summary>Registers the application-layer services into the DI container.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoomsApplication(this IServiceCollection services)
    {
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IWindowRegistry, WindowRegistry>();
        services.AddSingleton<ISwitchGate, SwitchGate>();
        services.AddSingleton<IRuleEnforcer, RuleEnforcer>();
        services.AddSingleton<IRoomManager, RoomManager>();
        services.AddSingleton<ISwitchOrchestrator, SwitchOrchestrator>();
        services.AddSingleton<IFocusSessionService, FocusSessionService>();
        services.AddSingleton<IFocusReassertionService, FocusReassertionService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IDefaultRoomsSeeder, DefaultRoomsSeeder>();
        services.AddSingleton<IStartupService, StartupService>();
        return services;
    }
}
