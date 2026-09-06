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

    /// <summary>Create the NAMED View of <paramref name="viewModel"/> and set its DataContext to it. One view-model can
    /// have several views registered against it - the same object read through different markup - and the key says
    /// which. A null key means the default view; a key that names nothing resolves to nothing.</summary>
    IUIComponent ResolveView(object viewModel, string viewKey);

    /// <summary>The View TYPE for a view-model type (no instance, no DataContext), or null.</summary>
    Type ResolveViewType(Type viewModelType);

    /// <summary>The View TYPE for a NAMED view of a view-model type. A null or empty key means the default view; a key
    /// that was never registered resolves to NULL rather than the default - falling back there would show a screen the
    /// caller did not ask for, and where the default view is the one hosting the region it nests that screen inside
    /// itself.</summary>
    Type ResolveViewType(Type viewModelType, string viewKey);

    /// <summary>Register one of several views for a view-model, under a key.</summary>
    void RegisterView(Type viewModelType, string viewKey, Type viewType);

    /// <summary>Register one of several views for a view-model, under a key.</summary>
    void RegisterView<TViewModel, TView>(string viewKey) where TView : class, IUIComponent;

    /// <summary>DI-resolve a bare View instance of <paramref name="viewType"/> (no DataContext) - used by the
    /// template-selector path where the hosting ContentPresenter adopts the DataContext itself.</summary>
    IUIComponent CreateViewInstance(Type viewType);

    void Register(Type viewModelType, Type viewType);
    void Register<TViewModel, TView>() where TView : class, IUIComponent;
    void RegisterFactory(Type viewModelType, Func<IUIComponent> factory);
    void RegisterKey(string key, Type viewModelType);
    bool TryResolveViewModelType(string key, out Type viewModelType);
}
