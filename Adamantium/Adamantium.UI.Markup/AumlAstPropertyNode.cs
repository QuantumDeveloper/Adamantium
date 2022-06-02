namespace Adamantium.UI.Markup;

public class AumlAstPropertyNode : AumlAstNode
{
    public AumlAstPropertyNode(
        IAumlLineInfo info, 
        IAumlAstNode property, 
        IAumlAstValueNode value) : base(info)
    {
        Property = property;
        Values = new List<IAumlAstValueNode>() { value };
    }
    
    public AumlAstPropertyNode(
        IAumlLineInfo info, 
        IAumlAstNode property, 
        IEnumerable<IAumlAstValueNode> values) : base(info)
    {
        Property = property;
        Values = values.ToList();
    }
    
    public IAumlAstNode Property { get; set; }
    
    public List<IAumlAstValueNode> Values { get; set; }

    public override string ToString()
    {
        return $"{Property}, Values: {Values.Count}";
    }
}