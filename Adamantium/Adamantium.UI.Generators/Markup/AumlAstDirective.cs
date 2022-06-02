namespace Adamantium.UI.Markup;

public class AumlAstDirective : AumlAstNode
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public IAumlAstValueNode Value { get; set; }
    
    public AumlAstDirective(IAumlLineInfo info, string ns, string name, IAumlAstValueNode value) : base(info)
    {
        Namespace = ns;
        Name = name;
        Value = value;
    }

    public override string ToString()
    {
        return $"{Namespace}:{Name}";
    }
}