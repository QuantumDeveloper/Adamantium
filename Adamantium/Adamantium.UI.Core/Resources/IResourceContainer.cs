namespace Adamantium.UI.Core.Resources;

public interface IResourceContainer
{
    void AddOrSetChildComponent(string key, object component);
    
    void RemoveChildComponent(string key);

    void RemoveAllChildComponents();
}