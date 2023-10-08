using System.Xml;
using System.Xml.Linq;
using Adamantium.Core;

namespace Adamantium.UI.Markup;

/// <summary>
/// Adamantium UI markup language parser
/// </summary>
public class AumlParser
{
    public static string BasicUiNamespace => "Adamantium.UI";

    public static string UiResourcesNamespace => "Adamantium.UI.Resources";

    public static AumlDocument Load(string path, bool parseTemplates = false)
    {
        var content = File.ReadAllText(path);
        return Parse(content, parseTemplates);
    }
    
    public static AumlDocument Parse(string aumlString, bool parseTemplates = false)
    {
        var root = XDocument.Parse(aumlString, LoadOptions.SetLineInfo).Root;

        var context = new ParserContext(root, parseTemplates);
        var rootNode = context.Parse();

        var doc = new AumlDocument()
        {
            Logger = context.Logger,
            HasErrors = context.HasErrors,
            Root = rootNode
        };
        
        return doc;
    }
}

public class ParserContext
{
    private readonly XElement rootElement;

    public Logger Logger { get; }
    
    public bool ParseTemplates { get; }
    
    public bool HasErrors { get; private set; }
    
    public ParserContext(XElement root, bool parseTemplates)
    {
        Logger = new Logger();
        ParseTemplates = parseTemplates;
        rootElement = root;
    }

    public IAumlAstNode Parse()
    {
        return ParseAumlNode(rootElement, true);
    }

    private IAumlAstTypeReference GetTypeReference(XElement element)
    {
        var ns = element.Name.Namespace.NamespaceName;
        bool isClrNamespace = ns.StartsWith("clr-namespace:");

        if (isClrNamespace)
        {
            string clrNamespace;
            string assembly = string.Empty;
            if (ns.Contains(';'))
            {
                var tokens = ns.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries);
                clrNamespace = tokens[0].Split(new[]{':'}, StringSplitOptions.RemoveEmptyEntries)[1];
                assembly = tokens[1].Split(new[]{'='}, StringSplitOptions.RemoveEmptyEntries)[1];
            }
            else
            {
                var tokens = ns.Split(':');
                clrNamespace = tokens[1];
            }
            
            return new AumlAstClrTypeReference(element.ToLineInfo(), clrNamespace, element.Name.LocalName, assembly);
        }
        if (ns == AumlNamespaces.AumlControls)
        {
            return new AumlAstClrTypeReference(element.ToLineInfo(), AumlParser.BasicUiNamespace, element.Name.LocalName, string.Empty);
        }
        else if (ns == AumlNamespaces.AumlResources)
        {
            return new AumlAstClrTypeReference(element.ToLineInfo(), AumlParser.UiResourcesNamespace, element.Name.LocalName, string.Empty);
        }

        return new AumlAstXmlTypeReference(element.ToLineInfo(), element.Name.NamespaceName, element.Name.LocalName);
    }

    private AumlAstObjectNode ParseAumlNode(XElement element, bool isRoot)
    {
        var type = GetTypeReference(element);
        if (type.Name == "ControlTemplate" && !ParseTemplates)
        {
            return new AumlAstTemplateNode(element.ToLineInfo(), type, element.ToString());
        }

        var objectNode = new AumlAstObjectNode(element.ToLineInfo(), type);

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                if (!isRoot) throw new Exception("xmlns could not be defined not at the root element");
            }
            else if (attribute.Name.NamespaceName.StartsWith("http://www.w3.org"))
            {
                // Silently ignore all xml-parser related attributes
            }
            else if (attribute.Name.NamespaceName == AumlNamespaces.AumlDirective)
            {
                objectNode.Children.Add(new AumlAstDirective(element.ToLineInfo(), attribute.Name.NamespaceName, attribute.Name.LocalName,
                    ParseTextOrMarkupExtension(attribute.Value, element, attribute.ToLineInfo())));
            }
            else // parse property or event
            {
                var propName = attribute.Name.LocalName;
                var ownerType = objectNode.Type;
                bool isAttachedProperty = false;

                // check does we faced with using of attached property or attached event
                if (propName.Contains('.'))
                {
                    isAttachedProperty = true;
                    var names = propName.Split('.');
                    propName = names[1];
                    // TODO we need to correctly get namespace for attached types.
                    // It will not work if attached property will not be from Adamantium.UI assembly
                    var ns = GetNamespaceForAttribute(attribute, element);
                    ownerType = new AumlAstXmlTypeReference(element.ToLineInfo(), type.Namespace, names[0]);
                }

                var propertyNode = new AumlAstPropertyNode(element.ToLineInfo(), 
                    new AumlAstPropertyReference(attribute.ToLineInfo(), isAttachedProperty, ownerType, type, propName),
                    ParseTextOrMarkupExtension(attribute.Value, element, attribute.ToLineInfo()));
                objectNode.Children.Add(propertyNode);
            }
        }

        foreach (var node in element.Nodes())
        {
            if (node is XElement elNode && elNode.Name.LocalName.Contains('.'))
            {
                var names = elNode.Name.LocalName.Split('.');
                var property = new AumlAstPropertyNode(
                    elNode.ToLineInfo(),
                    new AumlAstPropertyReference(
                        elNode.ToLineInfo(),
                        false,
                        new AumlAstXmlTypeReference(elNode.ToLineInfo(), elNode.Name.NamespaceName, names[1]),
                        type,
                        names[1]),
                    ParseValueNodes(elNode)
                    );
                
                objectNode.Children.Add(property);
            }
            else
            {
                var valueNode = ParseValueNode(node);
                if (valueNode != null)
                {
                    objectNode.Children.Add(valueNode);
                }
            }
        }

        return objectNode;
    }

    private IAumlAstValueNode ParseValueNode(XNode node)
    {
        if (node is XElement xElement)
            return ParseAumlNode(xElement, false);
        if (node is XText xText)
            return new AumlAstTextNode(node.ToLineInfo(), xText.Value);

        return null!;
    }

    private List<IAumlAstValueNode> ParseValueNodes(XElement element)
    {
        var valueNodes = new List<IAumlAstValueNode>();
        foreach (var xNode in element.Nodes())
        {
            var valueNode = ParseValueNode(xNode);
            if (valueNode != null)
                valueNodes.Add(valueNode);
        }

        return valueNodes;
    }

    private string GetNamespaceForAttribute(XAttribute attr, XElement element)
    {
        var ns = attr.Name.NamespaceName;
        return ns == string.Empty ? AumlNamespaces.AumlControls : ns;
    }

    private IAumlAstValueNode ParseTextOrMarkupExtension(string extension, XElement element, IAumlLineInfo info)
    {
        if (extension.StartsWith("{"))
        {
            
        }

        return new AumlAstTextNode(info, extension);
    }
}

public static class XElementExtension
{
    public static IAumlLineInfo ToLineInfo(this IXmlLineInfo info)
    {
        if (!info.HasLineInfo())
            throw new InvalidOperationException("XElement does not have line information");

        return new LineInfo(info);
    }
}

public class AumlDocument
{
    public Logger Logger { get; set; }
    
    public bool HasErrors { get; set; }
    
    public Dictionary<string, string> NamespaceAliases { get; set; } = new ();

    public IAumlAstNode Root { get; set; }
    
    public string RelativeFilePath { get; set; }

    public string FileName => Path.GetFileNameWithoutExtension(RelativeFilePath);
    
    public string RootNamespace { get; set; }
}

public static class AumlNamespaces
{
    public static string AumlDirective => "http://adamantium/ui/xaml/extensions";

    public static string AumlControls => "http://adamantium/ui";
    public static string AumlResources => "http://adamantium/ui/resources";

    // [assembly: XmlnsDefinition("http://adamantium/ui", "clr-namespace:Adamantium.UI.Controls;assembly:Adamantium.UI")]
    // [assembly: XmlnsDefinition("http://adamantium/ui/xaml/extensions", "clr-namespace:Adamantium.UI.Markup.MarkupExtensions;assembly:Adamantium.UI", "x")]
}