using System;
using System.Globalization;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Converters;

/// <summary>A COLOUR into a brush to paint with, and back.
/// <para>What a view-model holds is a colour - a value, which two objects cannot share and nobody can recolour behind
/// their back. A brush is what paints, and one is made HERE, per binding: writing into a brush a view-model handed over
/// is how one node's title strip recoloured a theme's accent and with it everything else wearing that colour.</para>
/// <para>Nothing to say - a colour nobody chose - leaves the target alone, so a property whose null means "whatever the
/// theme says" keeps saying it.</para></summary>
public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Color colour ? new SolidColorBrush(colour) : AdamantiumProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SolidColorBrush brush ? brush.Color : null;
}
