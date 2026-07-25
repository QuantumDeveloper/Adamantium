using System;
using System.Globalization;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.Data;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Maps a slider's double to a uniform <see cref="CornerRadius"/> so a demo tile's corner radius is drag-adjustable
/// from the view-model's plain double - keeping the CornerRadius UI-primitive out of the view-model.</summary>
public class DoubleToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => new CornerRadius(value is double d ? d : 0.0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is CornerRadius c ? c.TopLeft : 0.0;
}
