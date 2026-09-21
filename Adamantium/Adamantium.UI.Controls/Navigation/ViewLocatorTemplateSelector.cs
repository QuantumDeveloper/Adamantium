using System;
using System.Collections.Generic;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>A <see cref="DataTemplateSelector"/> that turns a view model into its View through the <see cref="IViewLocator"/>
/// - the framework drop-in for the sandbox TabViewSelector. Used for TabControl bodies / ItemsControl items where the
/// hosting presenter adopts the DataContext, so the produced View is left without one. Templates cached per type.</summary>
public sealed class ViewLocatorTemplateSelector : DataTemplateSelector
{
    private readonly IViewLocator _viewLocator;
    private readonly Dictionary<Type, DataTemplate> _cache = new();

    /// <summary>Parameterless ctor for XAML use - the locator is resolved from the app container lazily.</summary>
    public ViewLocatorTemplateSelector()
    {
    }

    public ViewLocatorTemplateSelector(IViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    public override DataTemplate SelectTemplate(object item, AdamantiumComponent container)
    {
        if (item == null) return null;

        var vmType = item.GetType();
        if (_cache.TryGetValue(vmType, out var cached)) return cached;

        var locator = _viewLocator ?? UIAppContext.Current?.UIContext?.Resolve<IViewLocator>();
        var viewType = locator?.ResolveViewType(vmType);
        if (viewType == null) return null;

        var template = new DataTemplate(() => new TemplateResult { RootComponent = locator.CreateViewInstance(viewType) });
        _cache[vmType] = template;
        return template;
    }
}
