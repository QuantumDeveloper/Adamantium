using System;
using System.Globalization;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Maps the selected <see cref="PatternType"/> to the <see cref="Visibility"/> of a live-pattern control, keyed by
/// ConverterParameter, so a control shows only for the pattern types that use it (e.g. the hatch-angle slider only for
/// Hatch). View-layer logic in a converter, not the view-model.</summary>
public class PatternParamVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value is PatternType t ? t : PatternType.Checkerboard;
        var visible = (parameter as string) switch
        {
            "Hatch" => type == PatternType.Hatch,
            _ => true
        };
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
}
