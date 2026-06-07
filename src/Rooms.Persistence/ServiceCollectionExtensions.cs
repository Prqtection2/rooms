using Microsoft.Extensions.DependencyInjection;
using Rooms.Core.Abstractions;

namespace Rooms.Persistence;

/// <summary>Registers the JSON persistence implementations into the DI container.</summary>
public static class ServiceCollectionExtensions
{
    /// <param name="rootDirectory">Base folder for room and settings files
    /// (e.g. %AppData%\Rooms).</param>
    public static IServiceCollection AddRoomsPersistence(this IServiceCollection services, string rootDirectory)
    {
        services.AddSingleton(new JsonStoreOptions { RootDirectory = rootDirectory });
        services.AddSingleton<IRoomStore, JsonRoomStore>();
        services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
        services.AddSingleton<IAppStateStore, JsonAppStateStore>();
        return services;
    }
}
