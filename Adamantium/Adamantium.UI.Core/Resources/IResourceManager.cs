namespace Adamantium.UI.Core.Resources;

public interface IResourceManager
{
    T FindResourceInScope<T>(string name, ResourceScope scope = ResourceScope.Local);
    
    object FindResourceInScope(string name, ResourceScope scope = ResourceScope.Local);
    
    object FindResource(string name);

    // Requester-aware: Local resources are visible only within the subtree of the element that declared them; walks up
    // from the requester. Context-less FindResource cannot see Local at all.
    object FindResource(IFundamentalUIComponent requester, string name);

    void AddSource(IAdamantiumComponent component, Type source, ResourceScope scope = ResourceScope.Local);
    
    void RemoveSources(IAdamantiumComponent component);
    
    Object this[string name] { get; }
}