namespace Adamantium.UI.Markup;

public class AumlAstTextNode : AumlAstNode, IAumlAstValueNode
{
    public string Text { get; set; }
    
    public AumlAstTextNode(IAumlLineInfo info, string text) : base(info)
    {
        Text = text;
        Type = new AumlAstXmlTypeReference(info, AumlNamespaces.AumlControls, "String");
    }

    public IAumlAstTypeReference Type { get; set; }

    public override string ToString()
    {
        return Text;
    }
}