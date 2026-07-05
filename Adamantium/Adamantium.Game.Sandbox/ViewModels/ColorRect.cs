namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One tile in the Layout tab's virtualized rectangle grid - just a fill colour string. The item template binds
/// it to Border.Background through a StringToBrushConverter (a {Binding} does no string->Brush conversion on its own).
/// Hundreds of these prove the ListBox/WrapPanel virtualization: only the on-screen ones are realized.</summary>
public sealed class ColorRect
{
    public string Color { get; init; }

    /// <summary>The SHARED stroke settings (same instance for every tile), so the sliders drive the whole grid: the tile
    /// template binds the shape's stroke through the nested path <c>Stroke.StrokeWidth</c> etc.</summary>
    public StrokeSettings Stroke { get; init; }
}
