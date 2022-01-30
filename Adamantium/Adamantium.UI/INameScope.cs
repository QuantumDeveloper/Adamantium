namespace Adamantium.UI;

public interface INameScope
{
    void RegisterName(string name, IUIComponent component);

    void Unregister(string name);

    IUIComponent Find(string name);
}