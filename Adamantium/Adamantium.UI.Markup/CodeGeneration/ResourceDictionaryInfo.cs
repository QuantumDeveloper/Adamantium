namespace Adamantium.UI.Markup.CodeGeneration;

public class ResourceDictionaryInfo(string uri, string fullTypeName)
{
    public string Uri { get; } = uri;

    public string FullTypeName { get; } = fullTypeName;
}