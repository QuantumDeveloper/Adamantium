namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One tile in the Layout tab's virtualized rectangle grid - just a fill colour string. The item template binds
/// it to Border.Background through a StringToBrushConverter (a {Binding} does no string->Brush conversion on its own).
/// Hundreds of these prove the ListBox/WrapPanel virtualization: only the on-screen ones are realized.</summary>
public sealed class ColorRect
{
    public string Color { get; init; }
}
