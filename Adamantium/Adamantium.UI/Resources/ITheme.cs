using System.Collections.Generic;
using Adamantium.Core.Collections;
using Adamantium.Engine.Core;
using Adamantium.UI.Media;

namespace Adamantium.UI.Resources;

public interface ITheme: IInitializable
{
    string Name { get; }
    
    Brush AccentColor { get; set; }
    
    ResourceDictionary Resources { get; }

    TrackingCollection<StyleSet> StyleSets { get; }

    void AddResource(StyleSet styleSet);
    
    StyleSet MergedStyles { get; }

    IEnumerable<Style> FindStylesForComponent(IFundamentalUIComponent component);
}