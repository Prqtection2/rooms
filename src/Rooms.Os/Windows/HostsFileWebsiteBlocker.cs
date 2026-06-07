using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;

namespace Rooms.Os.Windows;

/// <summary>
/// Website blocking via hosts-file rewriting (§9, Option B). Points blocked domains at
/// 127.0.0.1 inside a clearly-delimited managed block, then flushes the DNS cache. Requires
/// elevation; when not elevated it reports <see cref="IsAvailable"/> = false and no-ops.
///
/// Known bypasses (document in the UI): VPNs, DNS-over-HTTPS, and direct-IP access. This is a
/// deterrent, not a guarantee.
/// </summary>
public sealed class HostsFileWebsiteBlocker : IWebsiteBlocker
{
    private readonly string _hostsPath;
    private readonly ILogger<HostsFileWebsiteBlocker> _logger;

    public HostsFileWebsiteBlocker(ILogger<HostsFileWebsiteBlocker> logger)
    {
        _hostsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        _logger = logger;
    }

    public bool IsAvailable => IsElevated();

    public async Task ApplyAsync(IReadOnlyList<string> blockedDomains)
    {
        if (blockedDomains.Count == 0)
        {
            await ClearAsync().ConfigureAwait(false);
            return;
        }

        if (!IsAvailable)
        {
            // TODO (§9): gate Option B behind an explicit opt-in setting once one exists.
            _logger.LogInformation("Hosts-file website blocking unavailable (not elevated); skipping.");
            return;
        }

        await RewriteAsync(content => HostsFileEditor.ApplyManagedBlock(content, blockedDomains)).ConfigureAwait(false);
    }

    public async Task ClearAsync()
    {
        if (!IsAvailable)
            return;

        await RewriteAsync(HostsFileEditor.RemoveManagedBlock).ConfigureAwait(false);
    }

    private async Task RewriteAsync(Func<string, string> transform)
    {
        try
        {
            var content = File.Exists(_hostsPath)
                ? await File.ReadAllTextAsync(_hostsPath).ConfigureAwait(false)
                : string.Empty;

            var updated = transform(content);
            if (updated == content)
                return; // nothing changed

            await File.WriteAllTextAsync(_hostsPath, updated).ConfigureAwait(false);
            FlushDnsCache(); // fire-and-forget: don't make the caller wait on ipconfig
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to update the hosts file.");
        }
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void FlushDnsCache()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flush DNS cache.");
        }
    }
}
