namespace Adamantium.UI.Core.Media;

/// <summary>How a gradient paints the area OUTSIDE its [0,1] stop range (before the first stop / after the last).</summary>
public enum GradientSpreadMethod
{
    /// <summary>Clamp: the edge stop's colour extends outward (the default).</summary>
    Pad,
    /// <summary>Mirror the gradient repeatedly.</summary>
    Reflect,
    /// <summary>Tile the gradient repeatedly.</summary>
    Repeat
}
