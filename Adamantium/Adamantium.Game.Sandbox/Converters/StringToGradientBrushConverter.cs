using System;
using System.Globalization;
using Adamantium.Core.TypeParsing;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.Converters;

/// <summary>Builds a three-stop GRADIENT brush from a colour string, so the Layout tiles show off the linear/radial
/// gradient batch. To exercise BOTH kinds at once across the grid, the gradient TYPE is chosen deterministically from the
/// colour (so a given palette colour is always the same kind, but the palette mixes linear and radial). The stops go
/// light -> base -> dark, so every tile reads as a shaded, dimensional swatch instead of a flat fill.</summary>
public sealed class StringToGradientBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return null;
        var brush = TypeParser.Parse<Brush>(s);
        if (brush is not SolidColorBrush solid) return brush;
        var baseColor = solid.Color;
        var light = Lerp(baseColor, Color.FromRgba(255, 255, 255, 255), 0.4f);
        var dark = Lerp(baseColor, Color.FromRgba(0, 0, 0, 255), 0.4f);

        // Deterministic per-colour choice: mix linear and radial across the palette without any extra state.
        var radial = (HashString(s) & 1) == 0;
        var stops = new GradientStopCollection
        {
            new GradientStop(light, 0.0),
            new GradientStop(baseColor, 0.5),
            new GradientStop(dark, 1.0)
        };

        if (radial)
        {
            return new RadialGradientBrush(stops)
            {
                Center = new Vector2(0.5f, 0.5f),
                GradientOrigin = new Vector2(0.35f, 0.32f),   // off-centre highlight -> a soft "spotlight"
                RadiusX = 0.6,
                RadiusY = 0.6
            };
        }
        return new LinearGradientBrush(stops)
        {
            StartPoint = new Vector2(0f, 0f),
            EndPoint = new Vector2(1f, 1f)   // corner-to-corner diagonal
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Color Lerp(Color a, Color b, float t)
    {
        byte L(byte x, byte y) => (byte)(x + (y - x) * t);
        return Color.FromRgba(L(a.R, b.R), L(a.G, b.G), L(a.B, b.B), L(a.A, b.A));
    }

    // Small stable hash of the colour string (String.GetHashCode is randomised per run, which would flicker the type).
    private static int HashString(string s)
    {
        var h = 0;
        foreach (var ch in s) h = h * 31 + ch;
        return h & int.MaxValue;
    }
}
