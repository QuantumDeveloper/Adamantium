namespace Adamantium.UI.Markup.AST.TypeReference;

public class AumlAstXmlTypeReference : AumlAstNode, IAumlAstTypeReference
{
    public AumlAstXmlTypeReference(IAumlLineInfo info, string @namespace, string name) : base(info)
    {
        Name = name;
        Namespace = @namespace;
        Assembly = string.Empty;
    }

    public string Name { get; }
    public string Namespace { get; }
    public string Assembly { get; }

    public bool IsMarkupExtension { get; set; }

    public bool IsXmlNamespaceDeclaration => true;
    public bool IsResolved => false;

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
        return string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}/{Name}";
    }

    public bool ContainsValidData()
    {
        return !string.IsNullOrEmpty(Namespace);
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(Namespace) ? $"xml:{Name}" : $"xml:{Namespace}:{Name}";
    }
}