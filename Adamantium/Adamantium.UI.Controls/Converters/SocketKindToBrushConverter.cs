using System;
using System.Collections;
using System.Globalization;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Converters;

/// <summary>What a socket CARRIES into what it looks like, read off the catalogue of socket kinds the application gave
/// the canvas.
/// <para>A graph is read by colour, so the colour belongs to the kind and not to the socket: every socket carrying a
/// number looks the same, and nobody can pick the wrong shade for one of them. A kind the catalogue says nothing about
/// - and "anything", which is what an empty kind means - leaves the pin wearing the theme's.</para>
/// <para>INTERNAL, and that is the point. What an application says is the TABLE - which word means which colour, bound
/// to <see cref="InfiniteCanvas.SocketKinds"/> - and the canvas does the rest. Handing out the converter instead would
/// hand out a way to break every pin on the plane in exchange for nothing an application actually wants to say.</para>
/// </summary>
internal class SocketKindToBrushConverter : IValueConverter
{
    private readonly IEnumerable _kinds;

    public SocketKindToBrushConverter(IEnumerable kinds) => _kinds = kinds;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (_kinds == null || value is not string kind || string.IsNullOrEmpty(kind))
        {
            return AdamantiumProperty.UnsetValue;
        }

        foreach (var item in _kinds)
        {
            if (item is not ICanvasSocketKind entry || entry.Kind != kind) continue;

            return entry.Color is { } colour ? new SolidColorBrush(colour) : AdamantiumProperty.UnsetValue;
        }

        return AdamantiumProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        AdamantiumProperty.UnsetValue;
}
