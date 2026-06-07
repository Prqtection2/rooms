using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;

namespace Rooms.Os.Windows;

/// <summary>
/// Website blocking, Option A (§9): browser-process gating. The actual blocking is done by the
/// room's process rules (close/minimise the browser via the RuleEngine), so this blocker itself
/// is a no-op. Always available, no elevation. This is the default, swappable module.
/// </summary>
public sealed class BrowserGatingWebsiteBlocker : IWebsiteBlocker
{
    private readonly ILogger<BrowserGatingWebsiteBlocker> _logger;

    public BrowserGatingWebsiteBlocker(ILogger<BrowserGatingWebsiteBlocker> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable => true;

    public Task ApplyAsync(IReadOnlyList<string> blockedDomains)
    {
        if (blockedDomains.Count > 0)
            _logger.LogDebug(
                "Browser-gating mode: {Count} blocked domain(s) are enforced via the room's browser rule, not the hosts file.",
                blockedDomains.Count);

        return Task.CompletedTask;
    }

    public Task ClearAsync() => Task.CompletedTask;
}
