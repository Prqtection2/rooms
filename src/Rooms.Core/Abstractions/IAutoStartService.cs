namespace Rooms.Core.Abstractions;

/// <summary>
/// Controls whether Rooms launches at user sign-in. The Win32 implementation writes the
/// per-user Run registry key. (The higher-level startup orchestration that restores the
/// last room and runs the lost-window failsafe is the application-layer StartupService, §7.6.)
/// </summary>
public interface IAutoStartService
{
    bool IsEnabled();

    void Enable();

    void Disable();
}
