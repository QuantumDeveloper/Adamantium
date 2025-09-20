namespace Adamantium.UI.Markup.CodeGeneration;

public interface IResolvedMember
{
    string Name { get; }
    
    IResolvedType MemberType { get; }
    
    IResolvedType DeclaringType { get; }
    
    bool HasAttribute(string attributeMetadataName);

    bool HasSetter();
    
    ResolvedMemberKind MemberKind { get; }
}