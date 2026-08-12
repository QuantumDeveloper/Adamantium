using System;
using System.Globalization;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Turns one slider value into an even <see cref="Thickness"/> on all four sides, so a stand can drive a margin
/// from a plain double and the view-model stays free of UI primitives - the same split as
/// <see cref="DoubleToCornerRadiusConverter"/>.</summary>
public class DoubleToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => new Thickness(value is double d ? d : 0.0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Thickness t ? t.Left : 0.0;
}
