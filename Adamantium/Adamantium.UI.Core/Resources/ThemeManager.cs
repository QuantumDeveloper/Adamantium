using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;

namespace Adamantium.UI.Core.Resources;

public class ThemeManager : IThemeManager
{
    private IDependencyResolver dependencyResolver;
    private readonly Dictionary<string, ITheme> _themesMap;
    private Dictionary<StyleSelector, IUIComponent> components;
    private TrackingCollection<ITheme> _themes;
    private IResourceManager _resourceManager;

    public IReadOnlyList<ITheme> Themes => _themes;

    public ThemeManager(IDependencyResolver dependencyResolver)
    {
        this.dependencyResolver = dependencyResolver;
        _themes = new TrackingCollection<ITheme>();
        _themesMap = new Dictionary<string, ITheme>();
        components = new Dictionary<StyleSelector, IUIComponent>();
        _resourceManager = UIAppContext.Current.ResourceManager;
    }

    public void SetTheme(ITheme theme)
    {
        if (CurrentTheme == theme) return;

        // Palette activation is SYMMETRIC and LAZY: deactivate the outgoing theme's resource sources, then activate the
        // incoming one's. A theme does NOT register its palette at construction (see ResourceContext.SetSource) - only
        // the theme that is CURRENT has its palette live in the Theme provider, so N themes cost nothing until chosen.
        // This is the single owner of the "exactly one palette live" invariant.
        if (CurrentTheme != null) _resourceManager.RemoveSources(CurrentTheme);
        CurrentTheme = theme;
        ActivateThemeSources(theme);

        var windows = UIAppContext.Current.Windows;
        foreach (var window in windows)
        {
            window.InvalidateStyles();
        }

        // The restyle + resource re-resolution (brushes) above settles over the next few frames through paths that don't
        // all mark the render dirty, so force full render walks until the layout signals the cascade has fully drained -
        // otherwise re-styled controls stay blank until an unrelated mark (a mouse-over) forces a rebuild.
        RenderDirty.ForceStructuralUntilSettled();
    }

    // (Re)register the theme's linked palette into the resource manager. The ResourceLink authored as ResourceContext.Source
    // on the theme element is kept as an attached-property value, so we can re-add it every time the theme becomes current.
    private void ActivateThemeSources(ITheme theme)
    {
        if (theme is not AdamantiumComponent component) return;

        var link = ResourceContext.GetSource(component);
        if (link?.Source != null)
            _resourceManager.AddSource(component, link.Source, link.Scope);
    }

    public void ApplyTheme(ITheme theme, IFundamentalUIComponent component)
    {
        theme?.Initialize();
        ApplyStyles(theme, component);
    }

    public void ApplyTheme(string name, IFundamentalUIComponent component)
    {
        if (!_themesMap.TryGetValue(name, out var theme)) return;
        
        ApplyTheme(theme, component);
    }
    
    public void ApplyExternalStyles(IFundamentalUIComponent component, params Style[] styles)
    {
        if (styles.Length == 0) 
            return;
        
        component.AttachStyles(styles);
    }

    public void ApplyStyles(ITheme theme, IFundamentalUIComponent component)
    {
        if (theme == null) return;

        var styles = theme.FindStylesForComponent(component);
        if (styles.Length > 0)
        {
            component.AttachStyles(styles);
        }
    }

    public void ApplyStyles(IFundamentalUIComponent component)
    {
        var styles = FindStylesForComponent(component);
        component.AttachStyles(styles);
    }

    public void RemoveStyles(IFundamentalUIComponent component)
    {
        component.DetachStyles();
    }

    public Style[] FindStylesForComponent(IFundamentalUIComponent component)
    {
        if (CurrentTheme == null) return [];

        var styles = CurrentTheme.FindStylesForComponent(component);

        return styles;
    }

    public ITheme CurrentTheme { get; private set; }

    public void AddTheme(string name, ITheme theme)
    {
        if (!_themesMap.TryAdd(name, theme)) return;

        theme.Initialize();

        _themes.Add(theme);
    }

    public void RemoveTheme(string name)
    {
        _themesMap.Remove(name);
        var theme = _themes.FirstOrDefault(x => x.Name == name);
        if (theme != null)
        {
            _themes.Remove(theme);
        }
    }

    public ITheme this[string name] => !_themesMap.ContainsKey(name) ? null : _themesMap[name];

    public ITheme this[int index] => _themes[index];
    
    public void ApplyCurrentTheme(IFundamentalUIComponent control)
    {
        ApplyTheme(CurrentTheme, control);
    }
}