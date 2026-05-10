using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace GameServerApp.UI.Converters;

public class GameIdToIconConverter : IValueConverter
{
    private static readonly Dictionary<string, string> IconPaths = new()
    {
        ["minecraft"] = "avares://GameServerApp.UI/Assets/minecraft.png",
        ["paper"] = "avares://GameServerApp.UI/Assets/minecraft.png",
    };

    private static readonly Dictionary<string, Bitmap?> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string gameId || string.IsNullOrEmpty(gameId))
            return null;

        if (Cache.TryGetValue(gameId, out var cached))
            return cached;

        if (!IconPaths.TryGetValue(gameId, out var path))
            return null;

        try
        {
            var uri = new Uri(path);
            var asset = AssetLoader.Open(uri);
            var bitmap = new Bitmap(asset);
            Cache[gameId] = bitmap;
            return bitmap;
        }
        catch
        {
            Cache[gameId] = null;
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
