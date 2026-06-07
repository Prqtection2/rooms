namespace Rooms.Core.Abstractions;

/// <summary>User-facing notifications and Do-Not-Disturb control. (§5.4)</summary>
public interface INotificationService
{
    void ShowToast(string title, string message);

    /// <summary>Toggle Focus Assist / Do Not Disturb. Best-effort: there is no clean public
    /// API across Windows versions, so implementations may no-op and document it.</summary>
    void SetDoNotDisturb(bool enabled);
}
