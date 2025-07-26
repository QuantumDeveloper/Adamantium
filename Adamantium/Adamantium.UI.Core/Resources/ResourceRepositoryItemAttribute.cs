namespace Adamantium.UI.Core.Resources;

[AttributeUsage(AttributeTargets.Class)]
public class ResourceRepositoryItemAttribute : Attribute
{
    public string Path { get; }

    public ResourceRepositoryItemAttribute(string path)
    {
        Path = path;
    }
}