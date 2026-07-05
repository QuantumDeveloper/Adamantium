namespace Adamantium.UI.Core.Resources;

public class ResourceManager : IResourceManager
{
    private ResourceProvider _localResources { get; }
    private ResourceProvider _globalResources { get; }
    private ResourceProvider _themeResources { get; }
    
    public ResourceManager()
    {
        _localResources = new ResourceProvider();
        _globalResources = new ResourceProvider();
        _themeResources = new ResourceProvider();
    }

    private static readonly Lazy<Dictionary<string, Type>> _uriToTypeMap = new Lazy<Dictionary<string, Type>>(() =>
    {
        var map = new Dictionary<string, Type>();
        Initialize(map);
        return map;
    });

    private static void Initialize(Dictionary<string, Type> map)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            if (!assembly.IsDefined(typeof(ContainsResourcesAttribute), false)) 
                continue;
            try
            {
                var registrarType = assembly.GetTypes()
                    .FirstOrDefault(t => t.IsDefined(typeof(ResourceMapRegistrarAttribute), false));

                if (registrarType != null)
                {
                    var registerMethod = registrarType.GetMethod("Register",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                    if (registerMethod != null)
                    {
                        registerMethod.Invoke(null, [map]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Resource Scanner] Failed to process assembly {assembly.FullName}: {ex.Message}");
            }
        }
    }
    
    // Context-less lookup: Local scope is DELIBERATELY invisible here. A Local resource is only reachable through the
    // requester-aware overload below (which tree-scopes it), so a lookup with no requesting element - a global/eager
    // resolve - can never reach into a subtree's Local dictionary. Only Theme and Global are reachable this way.
    public object FindResource(string name)
    {
        if (_uriToTypeMap.Value.TryGetValue(name, out var type))
        {
            return type;
        }

        var resource = FindResourceInScope(name, ResourceScope.Theme);
        if (resource != null)
            return resource;

        return FindResourceInScope(name, ResourceScope.Global);
    }

    // Requester-aware lookup: Local resources are TREE-SCOPED. Walk up from the requesting element (logical parent, or
    // the visual parent where there's no logical one - e.g. templated content), and at each ancestor look only at
    // dictionaries that ancestor OWNS. Nearest owner wins; then fall back to Theme, then Global. This is what makes a
    // Local resource reachable from the subtree that declared it - and nowhere else.
    public object FindResource(IFundamentalUIComponent requester, string name)
    {
        if (_uriToTypeMap.Value.TryGetValue(name, out var type))
        {
            return type;
        }

        for (var node = requester; node != null; node = LogicalOrVisualParent(node))
        {
            var local = _localResources.FindResourceForOwner(node, name);
            if (local != null)
                return local;
        }

        var themeResource = FindResourceInScope(name, ResourceScope.Theme);
        if (themeResource != null)
            return themeResource;

        return FindResourceInScope(name, ResourceScope.Global);
    }

    private static IFundamentalUIComponent LogicalOrVisualParent(IFundamentalUIComponent node)
        => node.LogicalParent ?? (node as IUIComponent)?.VisualParent;

    public T FindResourceInScope<T>(string name, ResourceScope scope = ResourceScope.Local)
    {
        var resource = FindResourceInScope(name, scope);
        return (T)resource;
    }

    public object FindResourceInScope(string name, ResourceScope scope = ResourceScope.Local)
    {
        object value = null;
        switch (scope)
        {
            case ResourceScope.Local:
                value = _localResources.FindResource(name);
                break;
            case ResourceScope.Global:
                value = _globalResources.FindResource(name);
                break;
            case ResourceScope.Theme:
                value = _themeResources.FindResource(name);
                break;
        }
        
        return value;
    }

    public void AddSource(IAdamantiumComponent owner, Type source, ResourceScope scope = ResourceScope.Local)
    {
        switch (scope)
        {
            case ResourceScope.Local:
                _localResources.AddSource(owner, source);
                break;
            case ResourceScope.Global:
                _globalResources.AddSource(owner, source);
                break;
            case ResourceScope.Theme:
                _themeResources.AddSource(owner, source);
                break;
        }
    }

    public void RemoveSources(IAdamantiumComponent component)
    {
        if (component == null) return;
        
        _localResources.RemoveOwner(component);
        _themeResources.RemoveOwner(component);
        _globalResources.RemoveOwner(component);
    }

    public object this[string name] => FindResource(name);
}