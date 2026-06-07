using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;

namespace Rooms.App.Infrastructure;

/// <summary>
/// <see cref="INotificationService"/> backed by the tray icon (§5.4). Always logs; surfaces a
/// tray balloon once <see cref="AttachToastHandler"/> has been wired by the tray controller
/// (the tray icon doesn't exist yet when this service is constructed). Do-Not-Disturb has no
/// clean public API, so it is best-effort and currently logged only.
/// </summary>
public sealed class TrayNotificationService : INotificationService
{
    private readonly ILogger<TrayNotificationService> _logger;
    private Action<string, string>? _showToast;

    public TrayNotificationService(ILogger<TrayNotificationService> logger)
    {
        _logger = logger;
    }

    public void AttachToastHandler(Action<string, string> showToast) => _showToast = showToast;

    public void ShowToast(string title, string message)
    {
        _logger.LogInformation("Toast {Title}: {Message}", title, message);

        try
        {
            _showToast?.Invoke(title, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to display tray toast.");
        }
    }

    public void SetDoNotDisturb(bool enabled)
    {
        // TODO (ambient milestone): Focus Assist has no stable public API across Windows
        // versions. Best-effort - for now we record intent and suppress our own toasts.
        _logger.LogInformation("Do Not Disturb requested: {Enabled} (best-effort, not enforced).", enabled);
    }
}
