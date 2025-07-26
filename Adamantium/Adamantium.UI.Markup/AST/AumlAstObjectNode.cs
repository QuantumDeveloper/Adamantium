using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.AST;

public class AumlAstObjectNode : AumlAstNode, IAumlAstValueNode
{
    public AumlAstObjectNode(IAumlLineInfo info, IAumlAstTypeReference type): base(info)
    {
        TypeReference = type;
        Children = [];
        Arguments = [];
    }

    public IAumlAstTypeReference TypeReference { get; set; }
    
    public List<IAumlAstNode> Children { get; }
    
    public List<IAumlAstValueNode> Arguments { get; }

    public override string ToString()
    {
        return $"{TypeReference}. Children: {Children.Count}. Arguments: {Arguments.Count}";
    }
}