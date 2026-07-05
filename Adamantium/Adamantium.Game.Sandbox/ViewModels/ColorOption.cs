using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A palette entry for the template-bound DropDown: a name plus a swatch brush. Demonstrates an ItemTemplate
/// rendering a rich row (colour chip + label) instead of plain text, while SelectedColor stays the real object.</summary>
public sealed class ColorOption
{
    public ColorOption(string name, string hex)
    {
        Name = name;
        Swatch = new SolidColorBrush(hex);
    }

    public string Name { get; }

    public Brush Swatch { get; }
}
