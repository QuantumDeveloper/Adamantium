using System;
using System.Globalization;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Maps the selected <see cref="NoiseType"/> to the <see cref="Visibility"/> of a live-panel control group, keyed
/// by ConverterParameter, so a control shows only for the noise types that actually use it. Keeps the per-type UI logic in
/// the view layer (a converter) instead of the view-model. Keys: "Scale" and "Seed" (every type except CombustibleVoronoi,
/// which uses its own 3D field), "Fbm" (octaves/lacunarity/gain - every FBM type, not VoronoiBorders or Combustible),
/// "FirePalette" (CombustibleVoronoi only).</summary>
public class NoiseParamVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value is NoiseType t ? t : NoiseType.Simplex;
        var visible = (parameter as string) switch
        {
            "Scale" => type != NoiseType.CombustibleVoronoi,
            "Seed" => type != NoiseType.CombustibleVoronoi,
            "Fbm" => type != NoiseType.VoronoiBorders && type != NoiseType.CombustibleVoronoi,
            "FirePalette" => type == NoiseType.CombustibleVoronoi,
            _ => true
        };
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
}
