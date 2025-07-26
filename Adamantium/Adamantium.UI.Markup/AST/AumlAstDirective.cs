namespace Adamantium.UI.Markup.AST;

public class AumlAstDirective : AumlAstNode
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public IAumlAstValueNode Value { get; set; }
    
    public AumlAstObjectNode ParentNode { get; set; }
    
    public AumlAstDirective(IAumlLineInfo info, AumlAstObjectNode parent, string ns, string name, IAumlAstValueNode value) : base(info)
    {
        ParentNode = parent;
        Namespace = ns;
        Name = name;
        Value = value;
    }

    public override string ToString()
    {
        return $"{Namespace}:{Name}";
    }
}
