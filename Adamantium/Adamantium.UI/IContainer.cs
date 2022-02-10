namespace Adamantium.UI;

public interface IContainer
{
    void AddOrSetChildComponent(IMeasurableComponent component);

    void RemoveAllChildComponents();
}