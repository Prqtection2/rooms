namespace Rooms.Core.Abstractions;

/// <summary>
/// Pluggable website blocking (§9). Implementations are interchangeable: browser-process
/// gating (Option A, handled by the RuleEngine), hosts-file rewriting (Option B, needs admin),
/// or a companion browser extension (Option C, post-v1). All are best-effort deterrents.
/// </summary>
public interface IWebsiteBlocker
{
    /// <summary>Whether this blocker can act right now (e.g. false if it needs elevation
    /// and the app isn't elevated). Callers should surface this honestly in the UI.</summary>
    bool IsAvailable { get; }

    /// <summary>Block the given domains for the active room. No-op if unavailable.</summary>
    Task ApplyAsync(IReadOnlyList<string> blockedDomains);

    /// <summary>Remove any blocking this blocker applied.</summary>
    Task ClearAsync();
}
