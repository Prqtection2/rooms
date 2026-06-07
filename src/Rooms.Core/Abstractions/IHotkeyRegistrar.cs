using Rooms.Core.Models;

namespace Rooms.Core.Abstractions;

/// <summary>
/// Registers OS-level global hotkeys via RegisterHotKey and a dedicated message pump.
/// <see cref="HotkeyPressed"/> fires with the id returned by <see cref="Register"/>. (§5.3)
/// </summary>
public interface IHotkeyRegistrar
{
    /// <summary>Register a hotkey. Returns an id used to unregister and to correlate
    /// <see cref="HotkeyPressed"/>, or -1 if the combination could not be registered.</summary>
    int Register(HotkeyDefinition def);

    void Unregister(int id);

    event EventHandler<int>? HotkeyPressed;
}
