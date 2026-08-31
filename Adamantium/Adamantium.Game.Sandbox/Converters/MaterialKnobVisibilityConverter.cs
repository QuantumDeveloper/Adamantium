using System;
using System.Globalization;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Shows a material's knob only where it does something - a control that moves and changes nothing is worse
/// than one that is not there. Keyed by the KNOB rather than by the material, unlike its siblings here: which material
/// reads which number is a fact about the shaders, and it belongs in one place.</summary>
public class MaterialKnobVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var glass = value is MaterialType.LiquidGlass;
        var visible = parameter as string switch
        {
            "Blur" => !glass,
            "Refraction" => glass,
            // Only mica takes a picture of its own; the other two ARE what is beneath them.
            "Source" => value is MaterialType.Mica,
            _ => true
        };
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
}
