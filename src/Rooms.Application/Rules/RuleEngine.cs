using System.Text.RegularExpressions;
using Rooms.Core.Models;

namespace Rooms.Application.Rules;

/// <inheritdoc cref="IRuleEngine" />
public sealed class RuleEngine : IRuleEngine
{
    public bool IsOwnedBy(WindowInfo window, Room room) =>
        room.OwnedWindowMatchers.Any(matcher => Matches(matcher, window));

    public bool IsAllowed(WindowInfo window, Room room)
    {
        if (IsOwnedBy(window, room))
            return true;

        if (!room.Rules.BlockingEnabled)
            return true;

        return room.Rules.Mode switch
        {
            // Allowlist: only explicitly-allowed processes (plus owned windows and the
            // built-in safe-list) may stay - never block the shell or system UI (§8).
            BlockMode.Allowlist =>
                SafeProcessList.Contains(window.ProcessName) ||
                ContainsProcess(room.Rules.AllowedProcessNames, window.ProcessName),
            // Blocklist: everything stays except explicitly-blocked processes.
            BlockMode.Blocklist => !ContainsProcess(room.Rules.BlockedProcessNames, window.ProcessName),
            _ => true,
        };
    }

    public BlockReaction? GetBlockReaction(WindowInfo window, Room room)
    {
        if (!room.Rules.BlockingEnabled || IsAllowed(window, room))
            return null;

        return room.Rules.Reaction;
    }

    private static bool Matches(WindowMatcher matcher, WindowInfo window)
    {
        var hasCriterion = false;

        if (!string.IsNullOrWhiteSpace(matcher.ProcessName))
        {
            if (!string.Equals(matcher.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase))
                return false;
            hasCriterion = true;
        }

        if (!string.IsNullOrWhiteSpace(matcher.ExecutablePath))
        {
            if (!string.Equals(matcher.ExecutablePath, window.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                return false;
            hasCriterion = true;
        }

        if (!string.IsNullOrWhiteSpace(matcher.TitleRegex))
        {
            if (!TitleMatches(matcher.TitleRegex, window.Title))
                return false;
            hasCriterion = true;
        }

        // A matcher with no criteria is too broad to match anything.
        return hasCriterion;
    }

    private static bool TitleMatches(string pattern, string title)
    {
        try
        {
            return Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            // Invalid pattern never matches rather than throwing mid-switch.
            return false;
        }
    }

    private static bool ContainsProcess(IEnumerable<string> names, string processName) =>
        names.Any(n => string.Equals(n, processName, StringComparison.OrdinalIgnoreCase));
}
