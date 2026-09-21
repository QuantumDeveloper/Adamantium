namespace Adamantium.UI.Core.Media;

public static class BrushExtensions
{
    /// <summary>
    /// True when the brush would actually paint pixels: non-null and not a fully-transparent solid colour (alpha 0).
    /// A gradient or image brush counts as visible (its own content decides). Callers pass <c>Brushes.Transparent</c>
    /// or <c>null</c> to mean "no fill", so folding both here lets an invisible background cost zero render units - and
    /// makes a container hit-transparent over its empty areas (hit-testing is bounds-based). Safe on a null receiver.
    /// </summary>
    public static bool IsVisible(this Brush brush)
        => brush != null && !(brush is SolidColorBrush { Color.A: 0 });
}
