using System.Reflection;

namespace Adamantium.UI.Markup.CodeGeneration.Reflection;

public class ReflectionResolvedProperty : IResolvedProperty
{
    private readonly PropertyInfo _property;

    public ReflectionResolvedProperty(PropertyInfo property) => _property = property;

    public string Name => _property.Name;

    public IResolvedType PropertyType => new ReflectionResolvedType(_property.PropertyType);

    public bool HasAttribute(string attributeFullName) =>
        _property.GetCustomAttributes(false).Any(a => a.GetType().FullName == attributeFullName);
}
