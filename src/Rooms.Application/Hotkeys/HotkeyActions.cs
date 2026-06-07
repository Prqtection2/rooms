namespace Rooms.Application.Hotkeys;

/// <summary>Well-known <see cref="Core.Models.HotkeyBinding.ActionKey"/> values (§11.3 / §7.3).</summary>
public static class HotkeyActions
{
    /// <summary>Switch to the room named by the binding's RoomId.</summary>
    public const string SwitchToRoom = "switch-to-room";

    public const string NextRoom = "next-room";

    public const string PreviousRoom = "previous-room";

    public const string OpenSwitcher = "open-switcher";

    /// <summary>Start (or stop) a focus session on the active room.</summary>
    public const string ToggleFocus = "toggle-focus";
}
