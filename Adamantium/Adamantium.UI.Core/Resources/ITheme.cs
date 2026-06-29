using Adamantium.Graphics.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core.Resources;

public interface ITheme: IInitializable, IAdamantiumComponent
{
    string Name { get; }

    Brush AccentColor { get; set; }

    /// <summary>The theme's font for text - consumed in styles via {ThemeResource FontFamily} and inherited via
    /// UIComponent.FontFamily.</summary>
    FontFamily FontFamily { get; }
    
    StyleSetCollection StyleSets { get; }
    
    StyleIncludeCollection StyleIncludes { get; }

    void AddStyleSet(StyleSet styleSet);
    
    StyleSet MergedStyles { get; }

    Style[] FindStylesForComponent(IFundamentalUIComponent component);
    
    object GetResource(string key);
    
    bool TryGetResource(string key, out object value);
}