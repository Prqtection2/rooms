namespace Rooms.Core.Models;

/// <summary>
/// Modifier keys for a global hotkey. Values match the Win32 MOD_* flags (and WPF's
/// ModifierKeys), so the OS layer can pass them straight to RegisterHotKey.
/// Defined here to keep Core free of any WPF dependency.
/// </summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

/// <summary>A global hotkey: modifier flags plus a virtual-key code. (§5.3)</summary>
public sealed record HotkeyDefinition(ModifierKeys Modifiers, uint VirtualKey)
{
    public override string ToString() => $"{Modifiers}+VK_{VirtualKey:X2}";
}
