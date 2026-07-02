using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.Game.Sandbox;

/// <summary>
/// Maps a tab view-model to its View by naming convention (<c>FooViewModel</c> -&gt; a <c>FooView</c> type anywhere in
/// this assembly), wrapping the View in a <see cref="DataTemplate"/>. The Views carry no <c>x:ViewModel</c>: the hosting
/// <c>ContentPresenter</c> adopts the tab view-model as its DataContext (WPF ContentPresenter semantics), so each View's
/// {Binding}s resolve against its own view-model. Templates are cached per view-model type.
/// </summary>
public sealed class TabViewSelector : DataTemplateSelector
{
    private readonly Dictionary<Type, DataTemplate> _cache = new();

    public override DataTemplate SelectTemplate(object item, AdamantiumComponent container)
    {
        if (item is null) return null;

        var vmType = item.GetType();
        if (_cache.TryGetValue(vmType, out var cached)) return cached;

        var viewName = vmType.Name.Replace("ViewModel", "View");   // ButtonsViewModel -> ButtonsView
        var viewType = vmType.Assembly.GetTypes()
            .FirstOrDefault(t => t.Name == viewName && typeof(IUIComponent).IsAssignableFrom(t));
        if (viewType is null) return null;

        var template = new DataTemplate(() => new TemplateResult
        {
            RootComponent = (IUIComponent)Activator.CreateInstance(viewType)
        });
        _cache[vmType] = template;
        return template;
    }
}
