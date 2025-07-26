namespace Adamantium.UI.Core;

public interface IContainer
{
    void AddOrSetChildComponent(object component);

    void RemoveAllChildComponents();
}