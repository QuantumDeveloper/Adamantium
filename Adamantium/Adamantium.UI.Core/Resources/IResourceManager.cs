namespace Adamantium.UI.Core.Resources;

public interface IResourceManager
{
    T FindResourceInScope<T>(string name, ResourceScope scope = ResourceScope.Local);
    
    object FindResourceInScope(string name, ResourceScope scope = ResourceScope.Local);
    
    object FindResource(string name);

    void AddSource(IAdamantiumComponent component, Type source, ResourceScope scope = ResourceScope.Local);
    
    void RemoveSources(IAdamantiumComponent component);
    
    Object this[string name] { get; }
}