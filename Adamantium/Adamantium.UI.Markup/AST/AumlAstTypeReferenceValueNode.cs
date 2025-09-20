using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.AST;

public class AumlAstTypeReferenceValueNode : AumlAstNode, IAumlAstValueNode
{
    public AumlAstTypeReferenceValueNode(IAumlLineInfo info, IAumlAstTypeReference typeReference) : base(info)
    {
        TypeReference = typeReference;
    }

    public IAumlAstTypeReference TypeReference { get; set; }
}