namespace Adamantium.UI.Core.Media;

/// <summary>How a <see cref="GradientBrush"/> interpolates BETWEEN its stops.</summary>
public enum ColorInterpolationMode
{
    /// <summary>Interpolate in sRGB (WPF's behaviour). Simple, but muddies midpoints (a grey dead-zone between
    /// complementary colours) and can band on large fills.</summary>
    Srgb,

    /// <summary>Interpolate in OKLab (perceptually uniform). Smooth, even-brightness midpoints and no muddy greys - the
    /// modern look. Costs a per-fragment colour-space convert.</summary>
    Oklab
}
