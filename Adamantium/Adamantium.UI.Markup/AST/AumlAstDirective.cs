using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.AST;

public class AumlAstDirective : AumlAstNode, IAumlAstValueNode
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

    public IAumlAstTypeReference TypeReference { get; set; }

    // The owner comes from the caller: a directive points back at the object it sits on, and a copy must point at the
    // copy of it. Falls back to the original only when copied outside any object.
    public override IAumlAstNode Clone(AumlAstObjectNode parent) =>
        new AumlAstDirective(this, parent ?? ParentNode, Namespace, Name, (IAumlAstValueNode)Value?.Clone(parent))
        {
            TypeReference = TypeReference
        };
}
