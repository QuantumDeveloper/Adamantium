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