using System.Collections.Specialized;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core.Resources;

public class Theme : AdamantiumComponent, ITheme
{
    public Theme()
    {
        StyleSets = new StyleSetCollection();
        StyleIncludes = new StyleIncludeCollection();
        ResourceManager = UIAppContext.Current.ResourceManager;
        MergedStyles = new StyleSet();
    }
    
    public Theme(string name) : this()
    {
        Name = name;
    }

    public string Name { get; protected set; }
    
    public Brush AccentColor { get; set; }
    
    protected IResourceManager ResourceManager { get; }

    public StyleSet MergedStyles { get; }
    public Style[] FindStylesForComponent(IFundamentalUIComponent component)
    {
        if (component == null) return [];

        return MergedStyles.Styles.Where(x => x.Selector.Match(component)).ToArray();
    }

    public object GetResource(string key)
    {
        return ResourceManager.FindResource(key);
    }

    public bool TryGetResource(string key, out object value)
    {
        value = ResourceManager.FindResource(key);
        return value != null;
    }

    public StyleSetCollection StyleSets { get; }
    
    public StyleIncludeCollection StyleIncludes { get; }
    
    public void AddStyleSet(StyleSet styleSet)
    {
        StyleSets.Add(styleSet);
        MergedStyles.AddStyles(styleSet.Styles);
    }

    public void Initialize()
    {
        if (Initialized || Initializing) return;
        Initializing = true;

        foreach (var styleInclude in StyleIncludes)
        {
            var styleSet = (StyleSet)Activator.CreateInstance(styleInclude.Source);
            styleSet?.Initialize(this);
            StyleSets.Add(styleSet);
        }

        var styles = StyleSets.SelectMany(x=>x.Styles).ToList();
        MergedStyles.AddStyles(styles);
        StyleSets.CollectionChanged += OnStyleSetRepositoriesChanged;
        Initialized = true;
        Initializing = false;
    }

    public bool Initialized { get; private set; }
    
    public bool Initializing { get; private set; }

    private void OnStyleSetRepositoriesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        
    }
}