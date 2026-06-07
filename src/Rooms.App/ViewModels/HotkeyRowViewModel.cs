using CommunityToolkit.Mvvm.ComponentModel;
using Rooms.Core.Models;

namespace Rooms.App.ViewModels;

/// <summary>One editable hotkey binding row in Settings (§12.4): an action plus its captured combo.</summary>
public partial class HotkeyRowViewModel : ObservableObject
{
    public HotkeyRowViewModel(string actionLabel, string actionKey, Guid? roomId, HotkeyDefinition? hotkey)
    {
        ActionLabel = actionLabel;
        ActionKey = actionKey;
        RoomId = roomId;

        if (hotkey is not null)
        {
            _modifiers = hotkey.Modifiers;
            _virtualKey = hotkey.VirtualKey;
        }
    }

    public string ActionLabel { get; }

    public string ActionKey { get; }

    public Guid? RoomId { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(HasBinding))]
    private ModifierKeys _modifiers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(HasBinding))]
    private uint _virtualKey;

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private string? _conflict;

    public bool HasBinding => VirtualKey != 0;

    public string Display => HasBinding ? Describe(Modifiers, VirtualKey) : "(unset)";

    public HotkeyDefinition? ToDefinition() => HasBinding ? new HotkeyDefinition(Modifiers, VirtualKey) : null;

    private static string Describe(ModifierKeys modifiers, uint virtualKey)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyName(virtualKey));
        return string.Join("+", parts);
    }

    private static string KeyName(uint virtualKey)
    {
        var name = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)virtualKey).ToString();
        // "D1".."D9" -> "1".."9"
        if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1]))
            return name[1].ToString();
        return name;
    }
}
