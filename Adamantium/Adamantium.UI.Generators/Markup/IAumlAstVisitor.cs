namespace Adamantium.UI.Markup;

public interface IAumlAstVisitor
{
    IAumlAstNode Visit(IAumlAstNode node);

    void Push(IAumlAstNode node);

    void Pop();
}