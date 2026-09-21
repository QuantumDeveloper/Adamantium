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

    public override IAumlAstNode Clone(AumlAstObjectNode parent)
    {
        EnsureNotDerived(typeof(AumlAstObjectNode));

        var copy = new AumlAstObjectNode(this, TypeReference);
        CopyBodyInto(copy);
        return copy;
    }

    // The children point back at their owner, so each is copied against the COPY, not against this node.
    protected void CopyBodyInto(AumlAstObjectNode copy)
    {
        foreach (var child in Children)
        {
            copy.Children.Add(child.Clone(copy));
        }

        foreach (var argument in Arguments)
        {
            copy.Arguments.Add((IAumlAstValueNode)argument.Clone(copy));
        }
    }

    public override string ToString()
    {
        return $"{TypeReference}. Children: {Children.Count}. Arguments: {Arguments.Count}";
    }
}