namespace Adamantium.UI.Markup;

public class AumlAstTemplateNode : AumlAstObjectNode
{
    public AumlAstTemplateNode(IAumlLineInfo info, IAumlAstTypeReference type, string templateContent) : 
        base(info, type)
    {
        TemplateContent = templateContent;
    }

    public string TemplateContent { get; set; }
}