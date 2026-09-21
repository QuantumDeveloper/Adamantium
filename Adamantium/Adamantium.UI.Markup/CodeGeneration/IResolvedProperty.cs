namespace Adamantium.UI.Markup.CodeGeneration;

public interface IResolvedProperty
{
    string Name { get; }
    IResolvedType PropertyType { get; }
    bool HasAttribute(string attributeFullName);
}