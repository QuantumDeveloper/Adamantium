namespace Adamantium.UI.Markup;

public interface IAumlAstNode : IAumlLineInfo
{
    void VisitChildren(IAumlAstVisitor visitor);

    IAumlAstNode Visit(IAumlAstVisitor visitor);
}