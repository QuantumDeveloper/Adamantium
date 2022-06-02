namespace Adamantium.UI.Markup;

public class AumlAstXmlTypeReference : AumlAstNode, IAumlAstTypeReference
{
    public AumlAstXmlTypeReference(IAumlLineInfo info, string @namespace, string name) : base(info)
    {
        Name = name;
        Namespace = @namespace;
    }

    public string Name { get; set; }
    public string Namespace { get; set; }

    public bool IsMarkupExtension { get; set; }
    
    public bool IsEqual(IAumlAstTypeReference other)
    {
        if (other is AumlAstXmlTypeReference xml)
        {
            return xml.Name == Name && xml.Namespace == Namespace &&
                   xml.IsMarkupExtension == IsMarkupExtension;
        }

        return false;
    }

    public string GetFullTypeName()
    {
        return $"{Namespace}/{Name}";
    }

    public override string ToString()
    {
        return $"xml:{Namespace}:{Name}";
    }
}