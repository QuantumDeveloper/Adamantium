namespace Adamantium.UI.Core.Media;

/// <summary>Which escape-time fractal a <see cref="FractalBrush"/> renders (iterated per fragment in the SDF batch).</summary>
public enum FractalType
{
    /// <summary>z = z² + C for a fixed C (<see cref="FractalBrush.C"/>); the fragment is the starting z. Morphs beautifully
    /// as C moves - the auto-morph target.</summary>
    Julia,

    /// <summary>z = z² + c where c is the fragment; z starts at 0. The classic Mandelbrot set.</summary>
    Mandelbrot
}
