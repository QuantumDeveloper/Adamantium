using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Markup.CodeGeneration.Roslyn;

public class RoslynResolvedAttribute : IResolvedAttribute
{
    private readonly AttributeData _attributeData;
    
    public RoslynResolvedAttribute(AttributeData attributeData)
    {
        _attributeData = attributeData;
    }
    
    public string Name => _attributeData.AttributeClass?.ToDisplayString() ?? string.Empty;
    public IReadOnlyList<object> ConstructorArguments => _attributeData.ConstructorArguments
        .Select(ConvertTypedConstant)
        .ToList();
    
    public IReadOnlyDictionary<string, object> NamedArguments =>
        _attributeData.NamedArguments
            .ToDictionary(kv => kv.Key, kv => ConvertTypedConstant(kv.Value));
    
    private object ConvertTypedConstant(TypedConstant constant)
    {
        if (constant.Kind == TypedConstantKind.Array)
        {
            return constant.Values.Select(ConvertTypedConstant).ToArray();
        }

        return constant.Value;
    }
}