namespace Adamantium.UI.Markup;

public class AumlAstClrTypeReference : AumlAstNode, IAumlAstTypeReference
{
    public string Name { get; set; }
    public string Namespace { get; set; }
    public string Assembly { get; set; }
    
    public bool IsMarkupExtension { get; set; }
    
    public AumlAstClrTypeReference(IAumlLineInfo info, string @namespace, string name, string assembly) : base(info)
    {
        Name = name;
        Namespace = @namespace;
        Assembly = assembly;
    }
    
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

    public override string ToString()
    {
        return $"clr:{Namespace}:{Name}";
    }
}