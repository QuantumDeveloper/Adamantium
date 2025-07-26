namespace Adamantium.UI.Core;

public interface INameScope
{
    void RegisterName(string name, object component);

    void Unregister(string name);

    object Find(string name);
}