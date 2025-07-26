namespace Adamantium.UI.Markup.AST;

public interface IAumlAstNode : IAumlLineInfo
{
    void VisitChildren(IAumlAstVisitor visitor);

    IAumlAstNode Visit(IAumlAstVisitor visitor);
}