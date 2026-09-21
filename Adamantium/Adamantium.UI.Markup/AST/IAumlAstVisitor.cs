namespace Adamantium.UI.Markup.AST;

public interface IAumlAstVisitor
{
    IAumlAstNode Visit(IAumlAstNode node);

    void Push(IAumlAstNode node);

    void Pop();
}