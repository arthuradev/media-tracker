using System.Globalization;
using System.Windows.Data;

namespace MediaTracker.Converters;

public class EqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        return Equals(values[0]?.ToString(), values[1]?.ToString());
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EqualityToStyleConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4) return values.Length > 2 ? values[2] : null;

        bool isEqual = Equals(values[0]?.ToString(), values[1]?.ToString());
        // values[2] = active style, values[3] = inactive style
        return isEqual ? values[2] : values[3];
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
