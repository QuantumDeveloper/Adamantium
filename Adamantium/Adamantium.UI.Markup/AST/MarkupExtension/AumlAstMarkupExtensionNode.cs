using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.AST.MarkupExtension;

public class AumlAstMarkupExtensionNode : AumlAstNode, IAumlAstMarkupExtensionNode
{
    public AumlAstMarkupExtensionNode(IAumlLineInfo info, IAumlAstTypeReference typeReference) : base(info)
    {
        TypeReference = typeReference;
        Arguments = [];
    }
    
    public IAumlAstTypeReference TypeReference { get; set; }

    public List<IAumlAstMarkupExtensionArgument> Arguments { get; }
    
    public override string ToString()
    {
        return $"{TypeReference.Name}, Arguments: {Arguments.Count}";
    }
}

public class MarkupArgument : AumlAstNode, IAumlAstMarkupExtensionArgument
{
    public MarkupArgument(IAumlLineInfo info, string name, IAumlAstValueNode value) : base(info)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; set; }
    
    public IAumlAstValueNode Value { get; set; }

    public override string ToString()
    {
        return string.IsNullOrEmpty(Name) ? $"{Value}" : $"{Name} = {Value}";
    }
}

public class AumlAstMarkupExtensionLiteral : AumlAstTextNode, IAumlAstMarkupExtensionLiteral
{
    public AumlAstMarkupExtensionLiteral(IAumlLineInfo info, string text) : base(info, text)
    {
    }
}