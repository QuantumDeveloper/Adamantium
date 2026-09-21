namespace Adamantium.UI.Markup.AST.TypeReference;

public class AumlAstClrTypeReference : AumlAstNode, IAumlAstTypeReference
{
    public string Name { get; }
    public string Namespace { get; }
    public string Assembly { get; }
    public bool IsMarkupExtension { get; protected set; }
    
    public AumlAstClrTypeReference(IAumlLineInfo info, string @namespace, string name, string assembly) : base(info)
    {
        Name = name;
        Namespace = @namespace;
        Assembly = assembly;
        IsMarkupExtension = false;
    }

    public bool IsXmlNamespaceDeclaration => false;
    public virtual bool IsResolved => false;

    /// <summary>ITSELF - see <see cref="AumlAstXmlTypeReference.Clone"/>. Returning this is right for the resolved
    /// subclass too, which is why it does not override.</summary>
    public override IAumlAstNode Clone(AumlAstObjectNode parent) => this;

    public bool IsEqual(IAumlAstTypeReference other)
    {
        if (other is AumlAstClrTypeReference clr)
        {
            return clr.Namespace == Namespace && 
                   clr.Assembly == Assembly &&
                   clr.Name == Name &&
                   clr.IsMarkupExtension == IsMarkupExtension;
        }

        return false;
    }

    public string GetFullTypeName()
    {
        return $"{Namespace}.{Name}";
    }

    public bool ContainsValidData()
    {
        return !string.IsNullOrEmpty(Namespace) && !string.IsNullOrEmpty(Assembly);
    }

    public override string ToString()
    {
        return $"clr:{Namespace}:{Name}";
    }
}