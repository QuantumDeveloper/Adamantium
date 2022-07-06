using Adamantium.Core.DependencyInjection;
using System.Collections.Generic;

namespace Adamantium.UI;

public interface IThemeManager
{
    Theme CurrentTheme { get; }

    void ApplyTheme(Theme theme);

    void ApplyStyles(params Style[] styles);

    Theme this[string name] { get; }
}

public class ThemeManager : IThemeManager
{
    private IDependencyResolver dependencyResolver;
    private Dictionary<string, Theme> themes;
    private Dictionary<Selector, IFundamentalUIComponent> components;

    internal ThemeManager(IDependencyResolver dependencyResolver)
    {
        this.dependencyResolver = dependencyResolver;
        themes = new Dictionary<string, Theme>();
        components = new Dictionary<Selector, IFundamentalUIComponent>();
    }

    public void ApplyTheme(Theme theme)
    {
        CurrentTheme = theme;
    }

    public Theme CurrentTheme { get; private set; }

    public void ApplyStyles(params Style[] styles)
    {
        
    }

    public Theme this[string name] => !themes.ContainsKey(name) ? null : themes[name];
}