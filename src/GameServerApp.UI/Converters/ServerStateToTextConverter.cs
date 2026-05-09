using System.Globalization;
using Avalonia.Data.Converters;
using GameServerApp.Core.Models;

namespace GameServerApp.UI.Converters;

public class ServerStateToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ServerState state)
        {
            return state switch
            {
                ServerState.Running => "Running",
                ServerState.Starting => "Starting...",
                ServerState.Stopping => "Stopping...",
                ServerState.Error => "Error",
                _ => "Stopped"
            };
        }
        return "Unknown";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
