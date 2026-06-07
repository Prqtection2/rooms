using System.Globalization;
using System.Windows.Data;

namespace Rooms.App.Converters;

/// <summary>Adds one to an integer (turns a 0-based list index into a 1-based hotkey number).</summary>
public sealed class IncrementConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int i ? (i + 1).ToString(culture) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
