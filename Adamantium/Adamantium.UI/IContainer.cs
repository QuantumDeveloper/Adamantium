namespace Adamantium.UI;

public interface IContainer
{
    void AddOrSetChildComponent(IUIComponent component);

    void RemoveAllChildComponents();
}