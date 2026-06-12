using System.Reflection;

namespace Adamantium.UI.Markup.CodeGeneration.Reflection;

public class ReflectionResolvedAttribute : IResolvedAttribute
{
    private readonly Attribute _attribute;

    public ReflectionResolvedAttribute(Attribute attribute) => _attribute = attribute;

    public string Name => _attribute.GetType().FullName ?? string.Empty;

    // Reflection gives the constructed attribute instance, not its constructor call; expose its public
    // property/field values instead (sufficient for the transformer's needs).
    public IReadOnlyList<object> ConstructorArguments => Array.Empty<object>();

    public IReadOnlyDictionary<string, object> NamedArguments =>
        _attribute.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.DeclaringType != typeof(Attribute))
            .ToDictionary(p => p.Name, p => SafeGet(p, _attribute));

    private static object SafeGet(PropertyInfo p, object target)
    {
        try { return p.GetValue(target); } catch { return null; }
    }
}
