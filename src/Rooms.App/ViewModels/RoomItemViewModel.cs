using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Rooms.Core.Models;

namespace Rooms.App.ViewModels;

/// <summary>One room in the switcher list.</summary>
public partial class RoomItemViewModel : ObservableObject
{
    public RoomItemViewModel(Room room)
    {
        Id = room.Id;
        Name = room.Name;
        Accent = ParseBrush(room.AccentColorHex);
    }

    public Guid Id { get; }

    public string Name { get; }

    public Brush Accent { get; }

    [ObservableProperty]
    private bool _isActive;

    private static Brush ParseBrush(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                return brush;
            }
            catch (FormatException)
            {
                // fall through to default
            }
        }

        var fallback = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        fallback.Freeze();
        return fallback;
    }
}
