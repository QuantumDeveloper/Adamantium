using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.AST;

/// <summary>What <c>{x:Static local:Palette.Accent}</c> becomes: the value OF a static field or property, named at the
/// point of use. Until this existed a constant could only reach markup by being restated as a resource, so the one
/// declaration in C# and the one in the theme drifted apart on their own.</summary>
public class AumlAstStaticMemberValueNode : AumlAstNode, IAumlAstValueNode
{
    public AumlAstStaticMemberValueNode(IAumlLineInfo info, IAumlAstTypeReference typeReference, string memberName)
        : base(info)
    {
        TypeReference = typeReference;
        MemberName = memberName;
    }

    /// <summary>The type the member is declared on.</summary>
    public IAumlAstTypeReference TypeReference { get; set; }

    /// <summary>The static field or property to read.</summary>
    public string MemberName { get; }
}
