namespace Adamantium.UI.Markup.AST;

public class AumlAstPropertyNode : AumlAstNode
{
    public AumlAstPropertyNode(
        IAumlLineInfo info, 
        IAumlAstNode property, 
        IAumlAstValueNode value) : base(info)
    {
        Property = property;
        Values = [value];
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

    public override IAumlAstNode Clone(AumlAstObjectNode parent) =>
        new AumlAstPropertyNode(this, Property?.Clone(parent),
            Values.Select(value => (IAumlAstValueNode)value.Clone(parent)));

    public override string ToString()
    {
        return $"{Property}, Values: {Values.Count}";
    }
}