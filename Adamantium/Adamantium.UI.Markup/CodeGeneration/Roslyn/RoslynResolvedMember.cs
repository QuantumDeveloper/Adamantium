using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Markup.CodeGeneration.Roslyn;

public class RoslynResolvedMember : IResolvedMember
{
    private readonly ISymbol _symbol;

    public RoslynResolvedMember(ISymbol symbol, IResolvedType declaringType)
    {
        _symbol = symbol;
        DeclaringType = declaringType;
    }

    public string Name => _symbol.Name;

    public IResolvedType MemberType
    {
        get
        {
            return _symbol switch
            {
                IPropertySymbol prop => new RoslynResolvedType(prop.Type),
                IFieldSymbol field => new RoslynResolvedType(field.Type),
                IMethodSymbol method => new RoslynResolvedType(method.ReturnType),
                IEventSymbol evt => new RoslynResolvedType(evt.Type),
                _ => null
            };
        }
    }

    public IResolvedType DeclaringType { get; }

    public bool HasAttribute(string attributeMetadataName)
    {
        return _symbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() == attributeMetadataName);
    }
    
    public ResolvedMemberKind MemberKind =>
        _symbol.Kind switch
        {
            SymbolKind.Field => ResolvedMemberKind.Field,
            SymbolKind.Property => ResolvedMemberKind.Property,
            SymbolKind.Method => ResolvedMemberKind.Method,
            SymbolKind.Event => ResolvedMemberKind.Event,
            _ => ResolvedMemberKind.Unknown
        };
}