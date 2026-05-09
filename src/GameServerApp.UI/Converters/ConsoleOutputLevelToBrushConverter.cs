using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GameServerApp.Core.Models;

namespace GameServerApp.UI.Converters;

public class ConsoleOutputLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ConsoleOutputLevel level)
        {
            return level switch
            {
                ConsoleOutputLevel.Warning => new SolidColorBrush(Color.Parse("#EAB308")),
                ConsoleOutputLevel.Error => new SolidColorBrush(Color.Parse("#EF4444")),
                ConsoleOutputLevel.System => new SolidColorBrush(Color.Parse("#0EA5E9")),
                _ => new SolidColorBrush(Color.Parse("#E0E0E0"))
            };
        }
        return new SolidColorBrush(Color.Parse("#E0E0E0"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
