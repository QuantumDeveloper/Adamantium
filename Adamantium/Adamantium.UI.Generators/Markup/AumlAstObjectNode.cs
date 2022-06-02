using System.Collections.Generic;

namespace Adamantium.UI.Markup;

public class AumlAstObjectNode : AumlAstNode, IAumlAstValueNode
{
    public AumlAstObjectNode(IAumlLineInfo info, IAumlAstTypeReference type): base(info)
    {
        Type = type;
        Children = new List<IAumlAstNode>();
        Arguments = new List<IAumlAstValueNode>();
    }

    public IAumlAstTypeReference Type { get; set; }
    
    public List<IAumlAstNode> Children { get; }
    
    public List<IAumlAstValueNode> Arguments { get; }

    public override string ToString()
    {
        return $"{Type}. Children: {Children.Count}. Arguments: {Arguments.Count}";
    }
}