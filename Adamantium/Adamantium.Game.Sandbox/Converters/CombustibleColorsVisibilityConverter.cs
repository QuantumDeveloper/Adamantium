using System;
using System.Globalization;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Hides the colour pickers when the fire palette owns the colouring: collapses only when the type is
/// CombustibleVoronoi AND its fire palette is on (both inputs needed, hence a MultiBinding). value[0] = NoiseType,
/// value[1] = the fire-palette bool.</summary>
public class CombustibleColorsVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value.Length > 0 && value[0] is NoiseType t ? t : NoiseType.Simplex;
        var useFire = value.Length > 1 && value[1] is true;
        var hidden = type == NoiseType.CombustibleVoronoi && useFire;
        return hidden ? Visibility.Collapsed : Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
}
