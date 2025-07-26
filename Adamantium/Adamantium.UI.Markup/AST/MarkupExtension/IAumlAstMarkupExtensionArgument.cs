namespace Adamantium.UI.Markup.AST.MarkupExtension;

public interface IAumlAstMarkupExtensionArgument : IAumlAstNode
{
    public string Name { get; set; }
    
    public IAumlAstValueNode Value { get; set; }
}