using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DCSLogViewer.Models;

namespace DCSLogViewer.Converters;

/// <summary>
/// Converts a LogLevel to a foreground color for display.
/// </summary>
public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LogLevel level) return new SolidColorBrush(Color.FromRgb(110, 105, 140));

        return level switch
        {
            LogLevel.Trace => new SolidColorBrush(Color.FromRgb(140, 138, 165)),    // Muted gray-lavender
            LogLevel.Debug => new SolidColorBrush(Color.FromRgb(160, 158, 180)),  // Soft gray
            LogLevel.Info => new SolidColorBrush(Color.FromRgb(195, 192, 210)),   // Readable soft white
            LogLevel.Warning => new SolidColorBrush(Color.FromRgb(220, 185, 65)), // Clear amber
            LogLevel.Error => new SolidColorBrush(Color.FromRgb(225, 75, 100)),   // Clear rose
            LogLevel.Fatal => new SolidColorBrush(Color.FromRgb(235, 55, 85)),    // Hot pink
            LogLevel.Unknown => new SolidColorBrush(Color.FromRgb(150, 148, 170)),// Gray-lavender
            _ => new SolidColorBrush(Color.FromRgb(195, 192, 210))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a LogLevel to a background highlight for error/warning rows.
/// </summary>
public class LogLevelToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LogLevel level) return Brushes.Transparent;

        return level switch
        {
            LogLevel.Error => new SolidColorBrush(Color.FromArgb(40, 255, 45, 100)),
            LogLevel.Fatal => new SolidColorBrush(Color.FromArgb(55, 255, 40, 80)),
            LogLevel.Warning => new SolidColorBrush(Color.FromArgb(25, 255, 200, 50)),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a LogLevel enum to a short display label.
/// </summary>
public class LogLevelToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LogLevel level) return "???";

        return level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Fatal => "FTL",
            LogLevel.Unknown => "---",
            _ => "???"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a number/boolean to Visibility. Supports "invert" parameter.
/// 0, false, null = Collapsed (or Visible if inverted).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = value switch
        {
            bool b => b,
            int i => i > 0,
            double d => d > 0,
            null => false,
            _ => true
        };

        bool invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
        if (invert) isVisible = !isVisible;

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>
/// Converts a percentage (0-100) to a pixel width for drive usage bars.
/// Uses a fixed max width of 200px so the bar fits within the card.
/// </summary>
public class PercentToWidthConverter : IValueConverter
{
    public static readonly PercentToWidthConverter Instance = new();

    private const double MaxBarWidth = 200.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            return Math.Max(0, Math.Min(MaxBarWidth, (percent / 100.0) * MaxBarWidth));
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
