using System;
using System.Globalization;
using Adamantium.Game.Sandbox.ViewModels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Shows the one figure the live stands are set to paint on, keyed by ConverterParameter, so each stand holds a
/// Rectangle, an Ellipse and a Polygon and reveals whichever is selected. View-layer logic in a converter, not the
/// view-model.</summary>
public class PreviewShapeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var shape = value is PreviewShape s ? s : PreviewShape.Rectangle;
        var visible = Enum.TryParse<PreviewShape>(parameter as string, out var wanted) && shape == wanted;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
}
