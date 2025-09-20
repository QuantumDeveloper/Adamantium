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
    
    public object FindResource(string name)
    {
        if (_uriToTypeMap.Value.TryGetValue(name, out var type))
        {
            return type;
        }
        var resource = FindResourceInScope(name);
        if (resource != null) 
            return resource;
        
        resource = FindResourceInScope(name, ResourceScope.Theme);
        if (resource != null)
            return resource;
        
        resource = FindResourceInScope(name, ResourceScope.Global);
        return resource;
    }

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