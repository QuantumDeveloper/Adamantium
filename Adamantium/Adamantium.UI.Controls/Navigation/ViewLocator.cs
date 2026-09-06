using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Default <see cref="IViewLocator"/>. Resolution order: explicit factory, explicit map, then the naming
/// convention (searched in the view-model's assembly first, then all loaded assemblies, cached per type).</summary>
public sealed class ViewLocator : IViewLocator
{
    private readonly Dictionary<Type, Type> _map = new();
    private readonly Dictionary<Type, Func<IUIComponent>> _factories = new();
    private readonly Dictionary<Type, Type> _conventionCache = new();
    private readonly Dictionary<string, Type> _keys = new();

    // The several-views-per-view-model map, keyed by (view-model, key). Separate from _map on purpose: _map answers
    // "the view for this model", and that question still has to have ONE answer for everything that does not name a key.
    private readonly Dictionary<(Type, string), Type> _keyedViews = new();

    public void Register(Type viewModelType, Type viewType) => _map[viewModelType] = viewType;
    public void Register<TViewModel, TView>() where TView : class, IUIComponent => _map[typeof(TViewModel)] = typeof(TView);
    public void RegisterFactory(Type viewModelType, Func<IUIComponent> factory) => _factories[viewModelType] = factory;
    public void RegisterKey(string key, Type viewModelType) => _keys[key] = viewModelType;
    public bool TryResolveViewModelType(string key, out Type viewModelType) => _keys.TryGetValue(key, out viewModelType);

    public void RegisterView(Type viewModelType, string viewKey, Type viewType) => _keyedViews[(viewModelType, viewKey)] = viewType;

    public void RegisterView<TViewModel, TView>(string viewKey) where TView : class, IUIComponent
        => _keyedViews[(typeof(TViewModel), viewKey)] = typeof(TView);

    // A NAMED but unregistered view resolves to nothing, and deliberately: falling back to the model's default view is
    // what a caller means by "no key", not by "this key". The difference bites where the default view is the one HOSTING
    // the region - the fallback then puts a whole second copy of that screen inside itself, recursively, and the screen
    // appears twice with nothing to say why.
    public Type ResolveViewType(Type viewModelType, string viewKey)
    {
        if (viewModelType == null) return null;
        if (string.IsNullOrEmpty(viewKey)) return ResolveViewType(viewModelType);
        return _keyedViews.TryGetValue((viewModelType, viewKey), out var keyed) ? keyed : null;
    }

    public Type ResolveViewType(Type viewModelType)
    {
        if (viewModelType == null) return null;
        if (_map.TryGetValue(viewModelType, out var mapped)) return mapped;
        if (_conventionCache.TryGetValue(viewModelType, out var cached)) return cached;
        var byConvention = ResolveByConvention(viewModelType);
        _conventionCache[viewModelType] = byConvention;
        return byConvention;
    }

    public IUIComponent CreateViewInstance(Type viewType)
    {
        if (viewType == null) return null;
        var context = UIAppContext.Current?.UIContext;
        var instance = context != null ? context.Resolve(viewType) : Activator.CreateInstance(viewType);
        return instance as IUIComponent;
    }

    public IUIComponent ResolveView(object viewModel) => ResolveView(viewModel, null);

    public IUIComponent ResolveView(object viewModel, string viewKey)
    {
        if (viewModel == null) return null;
        var vmType = viewModel.GetType();

        // A named view wins over a factory: the factory was registered for the model as a whole, and the caller has
        // asked for one particular face of it.
        if (string.IsNullOrEmpty(viewKey) && _factories.TryGetValue(vmType, out var factory))
        {
            var made = factory();
            if (made != null) made.DataContext = viewModel;
            return made;
        }

        var viewType = ResolveViewType(vmType, viewKey);
        if (viewType == null) return null;
        var view = CreateViewInstance(viewType);
        if (view != null) view.DataContext = viewModel;
        return view;
    }

    private static Type ResolveByConvention(Type viewModelType)
    {
        var viewName = viewModelType.Name.Replace("ViewModel", "View");   // FooViewModel -> FooView

        var inSameAssembly = FindView(SafeGetTypes(viewModelType.Assembly), viewName);
        if (inSameAssembly != null) return inSameAssembly;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var found = FindView(SafeGetTypes(assembly), viewName);
            if (found != null) return found;
        }
        return null;
    }

    private static Type FindView(IEnumerable<Type> types, string viewName)
        => types.FirstOrDefault(t => t.Name == viewName && typeof(IUIComponent).IsAssignableFrom(t));

    private static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly assembly)
        => Adamantium.Core.Reflection.LoadableTypes.Of(assembly);
}
