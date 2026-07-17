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

    public void Register(Type viewModelType, Type viewType) => _map[viewModelType] = viewType;
    public void Register<TViewModel, TView>() where TView : class, IUIComponent => _map[typeof(TViewModel)] = typeof(TView);
    public void RegisterFactory(Type viewModelType, Func<IUIComponent> factory) => _factories[viewModelType] = factory;
    public void RegisterKey(string key, Type viewModelType) => _keys[key] = viewModelType;
    public bool TryResolveViewModelType(string key, out Type viewModelType) => _keys.TryGetValue(key, out viewModelType);

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

    public IUIComponent ResolveView(object viewModel)
    {
        if (viewModel == null) return null;
        var vmType = viewModel.GetType();

        if (_factories.TryGetValue(vmType, out var factory))
        {
            var made = factory();
            if (made != null) made.DataContext = viewModel;
            return made;
        }

        var viewType = ResolveViewType(vmType);
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

    private static Type[] SafeGetTypes(System.Reflection.Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch { return Array.Empty<Type>(); }
    }
}
