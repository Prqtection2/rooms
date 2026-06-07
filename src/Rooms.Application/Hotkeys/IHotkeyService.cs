namespace Rooms.Application.Hotkeys;

/// <summary>
/// Bridges OS-level global hotkeys to application actions (§7.3): per-room switch, next/previous
/// room, open the switcher overlay, toggle focus. Reloads bindings when settings change.
/// </summary>
public interface IHotkeyService
{
    /// <summary>Register all configured hotkey bindings from settings.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Re-read settings and re-register bindings (after the user edits hotkeys).</summary>
    Task ReloadAsync(CancellationToken ct = default);

    /// <summary>Temporarily release all hotkeys (so the settings UI can probe combos for
    /// availability). Call <see cref="ReloadAsync"/> to restore them.</summary>
    void Suspend();

    /// <summary>Raised when the open-switcher hotkey is pressed.</summary>
    event EventHandler? SwitcherHotkeyPressed;

    /// <summary>Raised when the toggle-focus hotkey is pressed.</summary>
    event EventHandler? ToggleFocusPressed;
}
