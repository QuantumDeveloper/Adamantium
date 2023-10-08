using System.Collections.Generic;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;

namespace Adamantium.UI.Resources;

public class Theme : AdamantiumComponent, ITheme
{
    public Theme(string name)
    {
        StyleSets = new TrackingCollection<StyleSet>();
        Resources = new ResourceDictionary();
        MergedStyles = new StyleSet();
        Name = name;
    }

    protected string[] ResourcePaths { get; set; }

    protected string[] StylesPaths { get; set; }

    public string Name { get; }
    
    public Brush AccentColor { get; set; }
    
    public ResourceDictionary Resources { get; }

    public StyleSet MergedStyles { get; }
    public IEnumerable<Style> FindStylesForComponent(IFundamentalUIComponent component)
    {
        if (component == null) return Enumerable.Empty<Style>();

        return MergedStyles.Styles.Where(x => x.Selector.Match(component));
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
        
        foreach(var resource in ResourcePaths)
        {
            var res = (ResourceDictionary)ResourceRepository.GetResourceDictionary(resource);
            res.Initialize();
            Resources.MergedDictionaries.Add(res);
        }

        foreach (var stylePath in StylesPaths)
        {
            var style = (StyleSet)ResourceRepository.GetStyleSet(stylePath);
            style.Initialize(this);
            StyleSets.Add(style);
        }

        var styles = StyleSets.SelectMany(x=>x.Styles).ToList();
        MergedStyles.AddStyles(styles);
        StyleSets.CollectionChanged += StyleRepositories_CollectionChanged;
        Initialized = true;
        Initializing = false;
    }

    public bool Initialized { get; private set; }
    
    public bool Initializing { get; private set; }

    private void StyleRepositories_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        
    }
}