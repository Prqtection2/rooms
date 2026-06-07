using Rooms.Core.Abstractions;
using Rooms.Core.Models;

namespace Rooms.Os.Windows;

/// <summary>
/// The <see cref="IWebsiteBlocker"/> the rest of the app sees. Delegates to the module chosen in
/// settings (§13, "swap-able"): Option A browser-gating (default) or Option B hosts-file. Always
/// clears the hosts file too, so switching modules never leaves a stale managed block behind.
/// </summary>
public sealed class WebsiteBlockerSelector : IWebsiteBlocker
{
    private readonly BrowserGatingWebsiteBlocker _browserGating;
    private readonly HostsFileWebsiteBlocker _hostsFile;
    private readonly IAppSettingsStore _settingsStore;

    private WebsiteBlockingMode _lastMode = WebsiteBlockingMode.BrowserGating;

    public WebsiteBlockerSelector(
        BrowserGatingWebsiteBlocker browserGating,
        HostsFileWebsiteBlocker hostsFile,
        IAppSettingsStore settingsStore)
    {
        _browserGating = browserGating;
        _hostsFile = hostsFile;
        _settingsStore = settingsStore;
    }

    public bool IsAvailable => Pick(_lastMode).IsAvailable;

    public async Task ApplyAsync(IReadOnlyList<string> blockedDomains)
    {
        var mode = await CurrentModeAsync().ConfigureAwait(false);

        if (mode == WebsiteBlockingMode.HostsFile)
        {
            await _hostsFile.ApplyAsync(blockedDomains).ConfigureAwait(false);
        }
        else
        {
            await _browserGating.ApplyAsync(blockedDomains).ConfigureAwait(false);
            await _hostsFile.ClearAsync().ConfigureAwait(false); // ensure no leftover hosts block
        }
    }

    public async Task ClearAsync()
    {
        await _browserGating.ClearAsync().ConfigureAwait(false);
        await _hostsFile.ClearAsync().ConfigureAwait(false); // always tidy the hosts file
    }

    private async Task<WebsiteBlockingMode> CurrentModeAsync()
    {
        var settings = await _settingsStore.LoadAsync().ConfigureAwait(false);
        _lastMode = settings.WebsiteBlocking;
        return _lastMode;
    }

    private IWebsiteBlocker Pick(WebsiteBlockingMode mode) =>
        mode == WebsiteBlockingMode.HostsFile ? _hostsFile : _browserGating;
}
