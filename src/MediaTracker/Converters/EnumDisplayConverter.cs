using System.Globalization;
using System.Windows.Data;
using MediaTracker.Models;

namespace MediaTracker.Converters;

public class MediaStatusDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is MediaStatus status ? status switch
        {
            MediaStatus.PlanToWatch => "Plan to Watch",
            MediaStatus.Watching => "Watching",
            MediaStatus.Completed => "Completed",
            MediaStatus.Paused => "Paused",
            MediaStatus.Dropped => "Dropped",
            _ => value.ToString() ?? string.Empty
        } : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class MediaTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is MediaType type ? type switch
        {
            MediaType.Series => "Series",
            MediaType.Anime => "Anime",
            MediaType.Movie => "Movie",
            MediaType.Game => "Game",
            _ => value.ToString() ?? string.Empty
        } : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class CompletionStateDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is CompletionState state ? state switch
        {
            CompletionState.NotStarted => "Not Started",
            CompletionState.InProgress => "In Progress",
            CompletionState.Completed => "Completed",
            CompletionState.HundredPercent => "100% Complete",
            CompletionState.Abandoned => "Abandoned",
            _ => value.ToString() ?? string.Empty
        } : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not MediaStatus status)
            return System.Windows.Application.Current.FindResource("TextMutedBrush");

        string key = status switch
        {
            MediaStatus.Watching => "StatusWatchingBrush",
            MediaStatus.Completed => "StatusDoneBrush",
            MediaStatus.Paused => "StatusPausedBrush",
            MediaStatus.Dropped => "StatusDroppedBrush",
            _ => "StatusWantBrush"
        };

        return System.Windows.Application.Current.FindResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ScoreToStarsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int score and > 0)
            return $"{score}/10";

        return "Not rated";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter is string s && s == "invert";
        bool hasValue = value is not null && (value is not string str || !string.IsNullOrEmpty(str));

        if (invert)
            hasValue = !hasValue;

        return hasValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter is string s && s == "invert";
        bool flag = value is bool b && b;

        if (invert)
            flag = !flag;

        return flag ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
