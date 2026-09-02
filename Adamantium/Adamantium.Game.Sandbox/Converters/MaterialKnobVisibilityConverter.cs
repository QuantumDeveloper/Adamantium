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
        var velvet = value is MaterialType.Velvet;
        var metal = value is MaterialType.Metal;
        var surface = velvet || metal;
        var visible = parameter as string switch
        {
            // A SURFACE has no capture to scatter, bend or tint: every knob about the thing BEHIND the element is
            // meaningless on it, and showing a control that does nothing is worse than showing none.
            "Blur" => !glass && !surface,
            "Refraction" => glass,
            "Tint" => !surface,
            // Film grain hides the banding an 8-bit CAPTURE brings. A surface captures nothing, so the control had
            // nothing to act on - a knob that does nothing is worse than no knob.
            "FilmGrain" => !surface,
            // The relief and the light belong to the whole surface branch; the rest is per material.
            "Surface" => surface,
            "Nap" => velvet,
            "Metal" => metal,
            // Only mica takes a picture of its own; the other two ARE what is beneath them.
            "Source" => value is MaterialType.Mica,
            _ => true
        };
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
}
