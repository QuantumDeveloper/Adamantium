namespace Adamantium.UI.Markup.AST;

public abstract class AumlAstNode : IAumlAstNode
{
    protected AumlAstNode(IAumlLineInfo info)
    {
        Line = info.Line;
        Position = info.Position;
    }
    
    public int Line { get; set; }
    public int Position { get; set; }
    
    /// <inheritdoc cref="IAumlAstNode.Clone"/>
    /// <remarks>Abstract on purpose: a node type added later does not compile until it can copy itself.</remarks>
    public abstract IAumlAstNode Clone(AumlAstObjectNode parent);

    // The one hole the compiler leaves: a derived node inherits this and copies itself as the base, losing its own
    // state. Called by each base that can be derived from.
    protected void EnsureNotDerived(System.Type self)
    {
        if (GetType() == self) return;

        throw new System.NotSupportedException(
            $"{GetType().Name} inherits {self.Name}.Clone and would be copied as a {self.Name}, losing its own state. " +
            "Override Clone in it.");
    }

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