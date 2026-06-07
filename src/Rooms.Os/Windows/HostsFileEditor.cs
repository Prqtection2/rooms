using System.Text;

namespace Rooms.Os.Windows;

/// <summary>
/// Pure string transforms for the hosts file's Rooms-managed block (§9, Option B). Kept
/// separate from file IO so the marker logic is unit-testable without touching the real
/// hosts file. The managed block is clearly delimited so user entries are never clobbered.
/// </summary>
public static class HostsFileEditor
{
    public const string BeginMarker = "# >>> ROOMS MANAGED >>>";
    public const string EndMarker = "# <<< ROOMS MANAGED <<<";

    private const string LoopbackV4 = "127.0.0.1";
    private const string NewLine = "\r\n";

    /// <summary>Return <paramref name="content"/> with a fresh managed block blocking the given
    /// domains (replacing any existing managed block). An empty domain list removes the block.</summary>
    public static string ApplyManagedBlock(string content, IReadOnlyList<string> domains)
    {
        var baseContent = RemoveManagedBlock(content).TrimEnd('\r', '\n', ' ', '\t');

        var normalized = NormalizeDomains(domains);
        if (normalized.Count == 0)
            return baseContent.Length == 0 ? string.Empty : baseContent + NewLine;

        var sb = new StringBuilder();
        if (baseContent.Length > 0)
            sb.Append(baseContent).Append(NewLine).Append(NewLine);

        sb.Append(BeginMarker).Append(NewLine);
        foreach (var domain in normalized)
            sb.Append(LoopbackV4).Append(' ').Append(domain).Append(NewLine);
        sb.Append(EndMarker).Append(NewLine);

        return sb.ToString();
    }

    /// <summary>Return <paramref name="content"/> with the Rooms-managed block removed,
    /// leaving all user entries intact. No-op if there is no managed block.</summary>
    public static string RemoveManagedBlock(string content)
    {
        if (string.IsNullOrEmpty(content) || !content.Contains(BeginMarker))
            return content;

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>(lines.Length);
        var inBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == BeginMarker)
            {
                inBlock = true;
                continue;
            }

            if (trimmed == EndMarker)
            {
                inBlock = false;
                continue;
            }

            if (!inBlock)
                kept.Add(line);
        }

        return string.Join(NewLine, kept).TrimEnd('\r', '\n', ' ', '\t') + NewLine;
    }

    private static List<string> NormalizeDomains(IReadOnlyList<string> domains)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in domains)
        {
            var domain = raw?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(domain))
                continue;

            Add(domain);
            if (!domain.StartsWith("www.", StringComparison.Ordinal))
                Add("www." + domain);
        }

        return result;

        void Add(string d)
        {
            if (seen.Add(d))
                result.Add(d);
        }
    }
}
