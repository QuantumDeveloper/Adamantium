namespace Adamantium.UI.Markup.AST;

public interface IAumlAstNode : IAumlLineInfo
{
    void VisitChildren(IAumlAstVisitor visitor);

    IAumlAstNode Visit(IAumlAstVisitor visitor);

    /// <summary>A copy of this node and everything under it, for a caller that is going to change it - the transform
    /// resolves types by writing back into the tree, and an incremental generator hands out the SAME tree it cached.
    ///
    /// <para><paramref name="parent"/> is the copy of the object node that owns this one: the two kinds that point back
    /// at their owner have to point at the COPY, or half the tree still refers to the original.</para></summary>
    IAumlAstNode Clone(AumlAstObjectNode parent);
}