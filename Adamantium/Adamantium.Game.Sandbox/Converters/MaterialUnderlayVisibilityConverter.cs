using System;
using System.Globalization;
using Adamantium.Game.Sandbox.ViewModels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Shows the one field the material stand is set to sit over, keyed by ConverterParameter - the stand holds all
/// of them and reveals the selected one. Same shape as <see cref="PreviewShapeVisibilityConverter"/>, and for the same
/// reason: view-layer logic belongs in a converter, not in the view-model.</summary>
public class MaterialUnderlayVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var underlay = value is MaterialUnderlay u ? u : MaterialUnderlay.LivingNoise;
        var visible = Enum.TryParse<MaterialUnderlay>(parameter as string, out var wanted) && underlay == wanted;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
}
