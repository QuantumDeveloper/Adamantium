using System;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Resolves a View for a view model (the reverse of the view-first <c>x:ViewModel</c>). Generalises the sandbox
/// TabViewSelector convention: explicit factory -&gt; explicit map -&gt; naming convention (<c>FooViewModel</c> -&gt;
/// <c>FooView</c>). View instances are DI-resolved (so a View may take constructor injection).</summary>
public interface IViewLocator
{
    /// <summary>Create the View for <paramref name="viewModel"/> AND set its DataContext to it.</summary>
    IUIComponent ResolveView(object viewModel);

    /// <summary>The View TYPE for a view-model type (no instance, no DataContext), or null.</summary>
    Type ResolveViewType(Type viewModelType);

    /// <summary>DI-resolve a bare View instance of <paramref name="viewType"/> (no DataContext) - used by the
    /// template-selector path where the hosting ContentPresenter adopts the DataContext itself.</summary>
    IUIComponent CreateViewInstance(Type viewType);

    void Register(Type viewModelType, Type viewType);
    void Register<TViewModel, TView>() where TView : class, IUIComponent;
    void RegisterFactory(Type viewModelType, Func<IUIComponent> factory);
    void RegisterKey(string key, Type viewModelType);
    bool TryResolveViewModelType(string key, out Type viewModelType);
}
