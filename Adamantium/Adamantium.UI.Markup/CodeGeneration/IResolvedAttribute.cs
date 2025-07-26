namespace Adamantium.UI.Markup.CodeGeneration;

public interface IResolvedAttribute
{
    string Name { get; }
    IReadOnlyList<object> ConstructorArguments { get; }
    IReadOnlyDictionary<string, object> NamedArguments { get; }
}