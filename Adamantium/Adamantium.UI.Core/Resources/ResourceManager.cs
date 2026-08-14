namespace Adamantium.UI.Core.Resources;

public class ResourceManager : IResourceManager
{
    private ResourceProvider _localResources { get; }
    private ResourceProvider _globalResources { get; }
    private ResourceProvider _themeResources { get; }

    // Coalesced change signal for live {ObservableResource}s. A mutation only sets the flag; the actual event fires once
    // per layout pass (FlushResourceChanges), so a theme swap - which removes the old sources then adds the new ones
    // during the pass's re-theme - notifies ONCE, after everything settled, and startup's bulk AddSource never storms.
    public event EventHandler ResourcesChanged;
    private bool _resourcesDirty;

    public void FlushResourceChanges()
    {
        if (!_resourcesDirty) return;
        _resourcesDirty = false;
        ResourcesChanged?.Invoke(this, EventArgs.Empty);
    }

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
                return Materialize(local);
        }

        var themeResource = FindResourceInScope(name, ResourceScope.Theme);
        if (themeResource != null)
            return themeResource;

        return FindResourceInScope(name, ResourceScope.Global);
    }

    // Logical parent first (bridging a template boundary via TemplatedParent, so template content still sees resources
    // declared on its host control), then the visual parent as a last resort for a node with neither. See
    // docs/TREE_MODEL_DESIGN.md.
    private static IFundamentalUIComponent LogicalOrVisualParent(IFundamentalUIComponent node)
        => node.GetLogicalParentOrBridge() ?? (node as IUIComponent)?.VisualParent;

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

        return Materialize(value);
    }

    // An entry declared x:Shared="False" is STORED as a factory, so every ask gets its own object - what a ContextMenu, a
    // Popup or a Transform needs, each belonging to the element it sits on. Unwrapped HERE, the one place a stored
    // resource leaves the dictionary, so no consumer has to know a factory was ever involved.
    private static object Materialize(object stored) => stored is PerTargetValue perTarget ? perTarget.Create() : stored;

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
        _resourcesDirty = true;
    }

    // Register an INLINE (per-element) dictionary instance - the ResourceContext.Resources authored right on an element -
    // rather than a shared dictionary type. Same scoping/lifecycle as a linked type.
    public void AddSource(IAdamantiumComponent owner, ResourceDictionary instance, ResourceScope scope = ResourceScope.Local)
    {
        switch (scope)
        {
            case ResourceScope.Local:
                _localResources.AddSource(owner, instance);
                break;
            case ResourceScope.Global:
                _globalResources.AddSource(owner, instance);
                break;
            case ResourceScope.Theme:
                _themeResources.AddSource(owner, instance);
                break;
        }
        _resourcesDirty = true;
    }

    public void NotifyResourcesChanged()
    {
        // A resource VALUE changed IN PLACE (an inline dictionary entry was reassigned) rather than a whole dictionary
        // being added/removed: drop the cached lookups so the next resolve re-reads, and mark dirty so the coalesced
        // ResourcesChanged fires this pass - live {ObservableResource}s then pick up the new value.
        _localResources.InvalidateCache();
        _themeResources.InvalidateCache();
        _globalResources.InvalidateCache();
        _resourcesDirty = true;
    }

    public bool SetResourceInScope(string name, object value, ResourceScope scope = ResourceScope.Local)
    {
        ResourceProvider provider;
        switch (scope)
        {
            case ResourceScope.Local:
                provider = _localResources;
                break;
            case ResourceScope.Global:
                provider = _globalResources;
                break;
            case ResourceScope.Theme:
                provider = _themeResources;
                break;
            default:
                return false;
        }

        if (!provider.SetResource(name, value)) return false;
        // The provider already evicted just the overridden key from ITS cache; mark dirty so the coalesced
        // ResourcesChanged fires this pass and live {ObservableResource}s re-resolve.
        _resourcesDirty = true;
        return true;
    }

    public void RemoveSources(IAdamantiumComponent component)
    {
        if (component == null) return;

        _localResources.RemoveOwner(component);
        _themeResources.RemoveOwner(component);
        _globalResources.RemoveOwner(component);
        _resourcesDirty = true;
    }

    public object this[string name] => FindResource(name);
}