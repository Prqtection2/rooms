using Microsoft.Extensions.DependencyInjection;
using Rooms.Core.Abstractions;
using Rooms.Os.Windows;

namespace Rooms.Os;

/// <summary>Registers the Win32 OS-interaction implementations into the DI container.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoomsOs(this IServiceCollection services)
    {
        services.AddSingleton<IWindowService, Win32WindowService>();
        services.AddSingleton<IProcessService, Win32ProcessService>();
        services.AddSingleton<IHotkeyRegistrar, Win32HotkeyRegistrar>();
        services.AddSingleton<IAutoStartService, RegistryAutoStartService>();
        services.AddSingleton<IWallpaperService, Win32WallpaperService>();

        // Website blocking (§9 / §13): default Option A (browser-gating), swappable to Option B
        // (hosts file) via settings. The selector delegates to the chosen module.
        services.AddSingleton<BrowserGatingWebsiteBlocker>();
        services.AddSingleton<HostsFileWebsiteBlocker>();
        services.AddSingleton<IWebsiteBlocker, WebsiteBlockerSelector>();
        return services;
    }
}
