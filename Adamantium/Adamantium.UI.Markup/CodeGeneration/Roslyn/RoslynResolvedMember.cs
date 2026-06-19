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

    /// <summary>The underlying Roslyn symbol — used by tooling (go-to-definition) to read source/metadata locations.</summary>
    public ISymbol Symbol => _symbol;

    public string Name => _symbol.Name;

    public IResolvedType MemberType
    {
        get
        {
            return _symbol switch
            {
                IPropertySymbol prop => new RoslynResolvedType(prop.Type),
                IFieldSymbol f => new RoslynResolvedType(f.Type),
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

    public bool HasSetter()
    {
        return _symbol switch
        {
            IPropertySymbol prop => prop.SetMethod is not null && 
                                    prop.SetMethod.DeclaredAccessibility == Accessibility.Public,

            IFieldSymbol field => !field.IsReadOnly && 
                                  field.DeclaredAccessibility == Accessibility.Public,

            _ => false
        };
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