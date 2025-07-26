using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.AST;

public class AumlAstTemplateNode : AumlAstObjectNode
{
    public AumlAstTemplateNode(IAumlLineInfo info, IAumlAstTypeReference type, string templateContent) : 
        base(info, type)
    {
        TemplateContent = templateContent;
    }

    public string TemplateContent { get; set; }
    
    public IAumlAstValueNode Ast { get; set; }
}