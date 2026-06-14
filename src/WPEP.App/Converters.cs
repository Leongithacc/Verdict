using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WPEP.App;

/// <summary>Resolves a token key ("Ok", "Warn", …) to the corresponding theme
/// brush, so semantic colors stay centralized in Theme.xaml.</summary>
public sealed class TokenBrushConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        value is string key && Application.Current.TryFindResource(key) is Brush brush
            ? brush
            : Brushes.Gray;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type _, object? __, CultureInfo ___)
    {
        bool truthy = value switch
        {
            bool b => b,
            int i => i > 0,
            _ => false,
        };
        return truthy ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) => value is not true;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => v is not true;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type _, object? __, CultureInfo ___) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}
