namespace Adamantium.UI.Markup;

public class AumlAstPropertyReference : AumlAstNode
{
    public IAumlAstTypeReference OwnerType { get; set; }
    
    public IAumlAstTypeReference TargetType { get; set; }
    
    public string Name { get; set; }
    
    public bool IsAttachedProperty { get; set; }
    
    public AumlAstPropertyReference(IAumlLineInfo info,
        bool isAttachedProperty,
        IAumlAstTypeReference ownerType,
        IAumlAstTypeReference targetType,
        string name) : base(info)
    {
        IsAttachedProperty = isAttachedProperty;
        OwnerType = ownerType;
        TargetType = targetType;
        Name = name;
    }

    public override string ToString()
    {
        return $"{Name}, TargetType: {TargetType}, OwnerType: {OwnerType}";
    }
}