using Adamantium.UI.Markup.AST.TypeReference;
using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.Markup.AST;

public class AumlAstTextNode : AumlAstNode, IAumlAstValueNode
{
    public string Text { get; set; }
    
    public AumlAstTextNode(IAumlLineInfo info, string text) : base(info)
    {
        Text = text;
        TypeReference = new AumlAstXmlTypeReference(info, AumlNamespaces.AumlControls, "String");
    }

    public IAumlAstTypeReference TypeReference { get; set; }

    public override string ToString()
    {
        return Text;
    }
}