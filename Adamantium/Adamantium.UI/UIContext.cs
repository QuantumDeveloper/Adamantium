using System;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI;

internal class UIContext : IUIContext
{
    internal UIContext(IDependencyResolver resolver, IUIApplication uiApplication)
    {
        Resolver = resolver;
        ThemeContext = uiApplication.ThemeManager;
        UIApplication = uiApplication;
    }
    public IDependencyResolver Resolver { get; }
    
    public IThemeContext ThemeContext { get; }
    
    public IUIApplication UIApplication { get; }

    public T Resolve<T>(string name = "")
    {
        return Resolver.Resolve<T>();
    }

    public object Resolve(Type type, string name = "")
    {
        return Resolver.Resolve(type, name);
    }
}