using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GameServerApp.Core.Models;

namespace GameServerApp.UI.Converters;

public class ServerStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ServerState state)
        {
            return state switch
            {
                ServerState.Running => new SolidColorBrush(Color.Parse("#22C55E")),
                ServerState.Starting or ServerState.Stopping => new SolidColorBrush(Color.Parse("#EAB308")),
                ServerState.Error => new SolidColorBrush(Color.Parse("#EF4444")),
                _ => new SolidColorBrush(Color.Parse("#555555"))
            };
        }
        return new SolidColorBrush(Color.Parse("#555555"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
