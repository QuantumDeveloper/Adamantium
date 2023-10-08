namespace Adamantium.UI;

public interface IContainer
{
    void AddOrSetChildComponent(object component);

    void RemoveAllChildComponents();
}