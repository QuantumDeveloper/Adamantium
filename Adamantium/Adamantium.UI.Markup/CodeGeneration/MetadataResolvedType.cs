using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.Markup.CodeGeneration;

public class MetadataResolvedType : IResolvedType
{
    private readonly AumlMetadataContainer _metadata;

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
            var baseTypeRef = _metadata.RootNode?.GetTypeReference();
            if (baseTypeRef is { IsResolved: true })
            {
                return _metadata.TypeResolver.Resolve(baseTypeRef.GetFullTypeName());
            }

            return null;
        }
    }

    public EntityType EntityType => _metadata.RootEntityType;
    public ResolvedTypeKind TypeKind => ResolvedTypeKind.Class;

    public IEnumerable<IResolvedMember> Members => [];
    public IResolvedMember GetMemberByName(string memberName) => null;
    public List<IResolvedProperty> GetAllProperties() => new List<IResolvedProperty>();
    public bool HasAttribute(string attributeName) => false;
    public IResolvedAttribute GetAttribute(string fullName) => null;
    public IEnumerable<IResolvedAttribute> GetAttributes() => [];
    public bool ImplementsInterface(string interfaceName) => false;
    public IResolvedType GetInterface(string interfaceName) => null;
    public bool IsCollection() => false;

    public bool FindPropertyWithAttribute(string attributeFullName, out IResolvedProperty property)
    {
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