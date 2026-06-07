namespace Rooms.Core.Models;

/// <summary>How a window should be shown when a room is activated.</summary>
public enum WindowShowState
{
    Normal,
    Minimized,
    Maximized,
}

/// <summary>
/// Saved position and size of a window, in virtual-screen pixels.
/// Used both to record where a window is and to restore it on room entry.
/// </summary>
public sealed class WindowPlacement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public WindowShowState ShowState { get; set; } = WindowShowState.Normal;
}
