namespace Adamantium.UI.Markup.AST;

public interface IAumlAstNode : IAumlLineInfo
{
    void VisitChildren(IAumlAstVisitor visitor);

    IAumlAstNode Visit(IAumlAstVisitor visitor);

    /// <summary>A copy of this node and everything under it - see <see cref="Parsers.AumlDocument.Clone"/> for why.
    /// <paramref name="parent"/> is the COPY of the owning object node, for the two kinds that point back at it.</summary>
    IAumlAstNode Clone(AumlAstObjectNode parent);
}