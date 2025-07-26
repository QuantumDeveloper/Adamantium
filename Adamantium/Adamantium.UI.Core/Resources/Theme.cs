using System.Collections.Specialized;
using Adamantium.Core.Collections;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core.Resources;

public class Theme : AdamantiumComponent, ITheme
{
    public Theme(string name)
    {
        StyleSets = new TrackingCollection<StyleSet>();
        Resources = new ResourceDictionary();
        MergedStyles = new StyleSet();
        Name = name;
    }

    public string Name { get; }
    
    public Brush AccentColor { get; set; }
    
    public ResourceDictionary Resources { get; }

    public StyleSet MergedStyles { get; }
    public Style[] FindStylesForComponent(IFundamentalUIComponent component)
    {
        if (component == null) return [];

        return MergedStyles.Styles.Where(x => x.Selector.Match(component)).ToArray();
    }

    public object GetResource(string key)
    {
        return Resources.FindName(key);
    }

    public TrackingCollection<StyleSet> StyleSets { get; }
    
    public void AddResource(StyleSet styleSet)
    {
        StyleSets.Add(styleSet);
        MergedStyles.AddStyles(styleSet.Styles);
    }

    public void Initialize()
    {
        if (Initialized || Initializing) return;
        Initializing = true;

        var styles = StyleSets.SelectMany(x=>x.Styles).ToList();
        MergedStyles.AddStyles(styles);
        StyleSets.CollectionChanged += StyleRepositories_CollectionChanged;
        Initialized = true;
        Initializing = false;
    }

    public bool Initialized { get; private set; }
    
    public bool Initializing { get; private set; }

    private void StyleRepositories_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        
    }
}