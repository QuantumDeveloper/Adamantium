using Adamantium.Core.DependencyInjection;
using System.Collections.Generic;

namespace Adamantium.UI;

public interface IThemeManager
{
    public void Apply()
    {

    }

    Theme this[string name] { get; }
}

public class ThemeManager : IThemeManager
{
    private Dictionary<string, Theme> themes;
    private IDependencyResolver dependencyResolver;

    internal ThemeManager(IDependencyResolver dependencyResolver)
    {
        this.dependencyResolver = dependencyResolver;
        themes = new Dictionary<string, Theme>();
    }

    public void Apply()
    {

    }

    public Theme this[string name] => !themes.ContainsKey(name) ? null : themes[name];
}