namespace Adamantium.UI.Markup;

public interface IAumlAstValueNode : IAumlAstNode
{
    IAumlAstTypeReference Type { get; set; }
}