using Adamantium.Core.Collections;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core.Resources;

public interface ITheme: IInitializable
{
    string Name { get; }
    
    Brush AccentColor { get; set; }
    
    ResourceDictionary Resources { get; }

    TrackingCollection<StyleSet> StyleSets { get; }

    void AddResource(StyleSet styleSet);
    
    StyleSet MergedStyles { get; }

    Style[] FindStylesForComponent(IFundamentalUIComponent component);
    
    object GetResource(string key);
}