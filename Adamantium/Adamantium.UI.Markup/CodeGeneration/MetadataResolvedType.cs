using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.Markup.CodeGeneration;

public class MetadataResolvedType : IResolvedType
{
    private readonly AumlMetadataContainer _metadata;
    private IResolvedType _baseType;
    private bool _baseResolved;

    public MetadataResolvedType(AumlMetadataContainer metadata)
    {
        _metadata = metadata;
    }

    public string Name => _metadata.ClassName;
    public string Namespace => _metadata.Namespace;
    public string FullName => _metadata.FullClassName;

    public string AssemblyName => _metadata.AssemblyName;

    public bool IsNamedType => true;
    public bool IsGenericType => false;
    public IEnumerable<IResolvedType> TypeArguments => [];

    public IResolvedType BaseType
    {
        get
        {
            if (_baseResolved) return _baseType;
            _baseResolved = true;
            var baseTypeRef = _metadata.RootNode?.GetTypeReference();
            if (baseTypeRef is { IsResolved: true })
                _baseType = _metadata.TypeResolver.Resolve(baseTypeRef.GetFullTypeName());
            return _baseType;
        }
    }

    public EntityType EntityType => _metadata.RootEntityType;
    public ResolvedTypeKind TypeKind => ResolvedTypeKind.Class;

    // A generated view declares no members of its own (it's non-extensible, extended via Behaviors). For type
    // resolution it IS its base (the <View>/<Window> root type), so delegate every member/interface query there: this
    // is what lets an embedded view expose the base control's settable properties - to markup (<ControlsView Width=".."/>)
    // and to the language server's property completion/hover.
    public IEnumerable<IResolvedMember> Members => BaseType?.Members ?? [];
    public IResolvedMember GetMemberByName(string memberName) => BaseType?.GetMemberByName(memberName);
    public List<IResolvedProperty> GetAllProperties() => BaseType?.GetAllProperties() ?? new List<IResolvedProperty>();
    public bool HasAttribute(string attributeName) => BaseType?.HasAttribute(attributeName) ?? false;
    public IResolvedAttribute GetAttribute(string fullName) => BaseType?.GetAttribute(fullName);
    public IEnumerable<IResolvedAttribute> GetAttributes() => BaseType?.GetAttributes() ?? [];
    public bool ImplementsInterface(string interfaceName) => BaseType?.ImplementsInterface(interfaceName) ?? false;
    public IResolvedType GetInterface(string interfaceName) => BaseType?.GetInterface(interfaceName);
    public bool IsCollection() => BaseType?.IsCollection() ?? false;

    public bool FindPropertyWithAttribute(string attributeFullName, out IResolvedProperty property)
    {
        if (BaseType != null)
            return BaseType.FindPropertyWithAttribute(attributeFullName, out property);
        property = null;
        return false;
    }

    public ResolvedSpecialType SpecialType => ResolvedSpecialType.None;
    public ResolvedMemberKind MemberKind => ResolvedMemberKind.Unknown;

    public bool InheritsFrom(string baseTypeName)
    {
        IResolvedType current = this;
        while (current != null)
        {
            if (current.FullName == baseTypeName)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    public bool IsAssignableTo(string fullName)
    {
        return InheritsFrom(fullName);
    }

    public bool InheritsFromMarkupExtension(string fullyQualifiedName)
    {
        return InheritsFrom(fullyQualifiedName);
    }

    public override string ToString()
    {
        return FullName;
    }
}