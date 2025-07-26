using System.Xml;
using System.Xml.Linq;
using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.Parsers;

public static class AumlParserExtensions
{
    public static IAumlAstTypeReference GetTypeReference(this XElement element)
    {
        var ns = element.Name.Namespace.NamespaceName;
        
        bool isClrNamespace = ns.StartsWith("clr-namespace:");

        if (isClrNamespace)
        {
            var clrNamespace = ParseXmlNamespace(ns);
            
            return new AumlAstClrTypeReference(element.ToLineInfo(), clrNamespace.Namespace, element.Name.LocalName, clrNamespace.Assembly);
        }
        
        return new AumlAstXmlTypeReference(element.ToLineInfo(), element.Name.NamespaceName, element.Name.LocalName);
    }

    public static NamespaceMapping GetNamespaceMapping(this XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration)
        {
            string prefix = attribute.Name.LocalName == "xmlns" ? "" : attribute.Name.LocalName;
            string nsValue = attribute.Value;

            var mapping = new NamespaceMapping { Prefix = prefix };

            if (nsValue.StartsWith("clr-namespace:"))
            {
                // clr-namespace:MyApp.Converters;assembly=MyLib
                var clrNs = ParseXmlNamespace(nsValue);
                mapping.Namespace = clrNs.Namespace;
                mapping.Assembly = clrNs.Assembly;
                mapping.IsClrNamespace = true;
            }
            else
            {
                mapping.Namespace = nsValue;
            }

            return mapping;
        }
        
        return null;
    }
    
    public static IAumlLineInfo ToLineInfo(this IXmlLineInfo info)
    {
        if (!info.HasLineInfo())
            throw new InvalidOperationException("XElement does not have line information");

        return new LineInfo(info);
    }

    public static ClrNamespaceData ParseXmlNamespace(this string @namespace)
    {
        var clrNs = new ClrNamespaceData();
        var parts = @namespace.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("clr-namespace:"))
                clrNs.Namespace = part.Substring("clr-namespace:".Length);
            else if (part.StartsWith("assembly="))
                clrNs.Assembly = part.Substring("assembly=".Length);
        }

        return clrNs;
    }
}

public class ClrNamespaceData
{
    public string Namespace { get; set; }
    
    public string Assembly { get; set; }
}