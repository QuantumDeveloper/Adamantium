using System.Collections.Generic;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;

namespace Adamantium.UI.Resources;

public class ThemeManager : IThemeManager
{
    private IDependencyResolver dependencyResolver;
    private readonly Dictionary<string, ITheme> _themesMap;
    private Dictionary<Selector, IUIComponent> components;
    private TrackingCollection<ITheme> _themes;

    public IReadOnlyList<ITheme> Themes => _themes;

    internal ThemeManager(IDependencyResolver dependencyResolver)
    {
        this.dependencyResolver = dependencyResolver;
        _themes = new TrackingCollection<ITheme>();
        _themesMap = new Dictionary<string, ITheme>();
        components = new Dictionary<Selector, IUIComponent>();
    }

    public void ApplyTheme(ITheme theme, IUIComponent component)
    {
        if (theme == null) return;
        
        theme.Initialize();
        CurrentTheme = theme;
        ApplyStyles(component);
    }

    public void ApplyTheme(string name, IUIComponent component)
    {
        if (!_themesMap.TryGetValue(name, out var theme)) return;
        
        theme.Initialize();
        CurrentTheme = theme;
        ApplyStyles(component);
    }
    
    public void ApplyStyles(IUIComponent component, params Style[] styles)
    {
        var stack = new Stack<IUIComponent>();
        stack.Push(component);
        while (stack.Count > 0)
        {
            var control = stack.Pop();
            control.AttachStyles(styles);
        
            var children = control.GetVisualDescendants();
            foreach (var child in children)
            {
                stack.Push(child);
            }
        }
    }

    public void ApplyStyles(IUIComponent component)
    {
        var styles = FindStylesForComponent(component);
        component.AttachStyles(styles.ToArray());
    }

    public void RemoveStyles(IUIComponent component)
    {
        component.DetachStyles();
    }

    public IEnumerable<Style> FindStylesForComponent(IUIComponent component)
    {
        if (CurrentTheme == null) return Enumerable.Empty<Style>();

        var styles = CurrentTheme.FindStylesForComponent(component);

        return styles;
    }

    public ITheme CurrentTheme { get; private set; }

    public void AddTheme(string name, ITheme theme)
    {
        if (_themesMap.ContainsKey(name)) return;
        
        _themesMap[name] = theme;
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
}