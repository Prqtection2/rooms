namespace Rooms.Core.Models;

/// <summary>
/// How the app decides whether an existing top-level window belongs to a room. Matching
/// is by any combination of these fields; all non-null fields must match (AND). (§4.3)
/// </summary>
public sealed class WindowMatcher
{
    /// <summary>Process image name, e.g. "chrome", "Acrobat".</summary>
    public string? ProcessName { get; set; }

    /// <summary>Optional window-title pattern (regular expression).</summary>
    public string? TitleRegex { get; set; }

    /// <summary>Full executable path match.</summary>
    public string? ExecutablePath { get; set; }
}
