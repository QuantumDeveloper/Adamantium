using System.Collections.Generic;

namespace Adamantium.UI.Markup;

public abstract class AumlAstNode : IAumlAstNode
{
    protected AumlAstNode(IAumlLineInfo info)
    {
        Line = info.Line;
        Position = info.Position;
    }
    
    public int Line { get; set; }
    public int Position { get; set; }
    
    public virtual void VisitChildren(IAumlAstVisitor visitor)
    {
    }

    public IAumlAstNode Visit(IAumlAstVisitor visitor)
    {
        var node = visitor.Visit(this);
        try
        {
            visitor.Push(node);
            node.VisitChildren(visitor);
        }
        finally
        {
            visitor.Pop();
        }
        
        return node;
    }

    protected static void VisitList<T>(IList<T> list, IAumlAstVisitor visitor) where T : IAumlAstNode
    {
        for (int i = 0; i < list.Count; i++)
        {
            list[i] = (T)list[i].Visit(visitor);
        }
    }
}