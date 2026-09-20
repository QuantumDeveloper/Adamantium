using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.AST;

public class AumlAstPropertyReference : AumlAstNode
{
    public IAumlAstTypeReference OwnerType { get; set; }
    
    public IAumlAstTypeReference TargetType { get; set; }
    
    public string Name { get; set; }
    
    public bool IsAttachedProperty { get; set; }
    
    public AumlAstObjectNode ParentNode { get; }
    
    public AumlAstPropertyReference(IAumlLineInfo info,
        bool isAttachedProperty,
        AumlAstObjectNode parentNode,
        IAumlAstTypeReference ownerType,
        IAumlAstTypeReference targetType,
        string name) : base(info)
    {
        ParentNode = parentNode;
        IsAttachedProperty = isAttachedProperty;
        OwnerType = ownerType;
        TargetType = targetType;
        Name = name;
    }

    // Points back at the object it belongs to, so the copy takes the copy of that object.
    public override IAumlAstNode Clone(AumlAstObjectNode parent) =>
        new AumlAstPropertyReference(this, IsAttachedProperty, parent ?? ParentNode, OwnerType, TargetType, Name);

    public override string ToString()
    {
        return $"{Name}, TargetType: {TargetType}, OwnerType: {OwnerType}";
    }
}