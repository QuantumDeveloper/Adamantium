using System;
using System.Globalization;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Maps a slider's double to a SQUARE tile at the origin - a <see cref="TileBrush.Viewport"/> a demo can drag.
/// One number for both axes because a square tile is what a texture is normally drawn as, and it keeps the Rect UI
/// primitive out of the view-model.</summary>
public class DoubleToSquareViewportConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => new Rect(0, 0, value is double d ? d : 1.0, value is double h ? h : 1.0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Rect r ? r.Width : 0.0;
}
