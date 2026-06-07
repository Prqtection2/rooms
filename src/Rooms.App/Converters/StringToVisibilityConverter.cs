using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Rooms.App.Converters;

/// <summary>Visible when the bound string is non-empty; Collapsed otherwise (for the conflict label).</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
