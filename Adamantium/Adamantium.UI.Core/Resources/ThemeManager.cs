using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;

namespace Adamantium.UI.Core.Resources;

public class ThemeManager : IThemeManager
{
    private IDependencyResolver dependencyResolver;
    private readonly Dictionary<string, ITheme> _themesMap;
    private Dictionary<Selector, IUIComponent> components;
    private TrackingCollection<ITheme> _themes;
    private IResourceManager _resourceManager;

    public IReadOnlyList<ITheme> Themes => _themes;

    public ThemeManager(IDependencyResolver dependencyResolver)
    {
        this.dependencyResolver = dependencyResolver;
        _themes = new TrackingCollection<ITheme>();
        _themesMap = new Dictionary<string, ITheme>();
        components = new Dictionary<Selector, IUIComponent>();
        _resourceManager = UIAppContext.Current.ResourceManager;
    }

    public void SetTheme(ITheme theme)
    {
        if (CurrentTheme == theme) return;
        
        _resourceManager.RemoveSources(CurrentTheme);
        CurrentTheme = theme;
        var windows = UIAppContext.Current.Windows;
        foreach (var window in windows)
        {
            window.InvalidateStyles();
        }
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