using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.AST;

public interface IAumlAstValueNode : IAumlAstNode
{
    IAumlAstTypeReference TypeReference { get; set; }
}