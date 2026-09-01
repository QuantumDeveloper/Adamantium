using System.Xml.Linq;
using Adamantium.Core;
using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.Parsers;

/// <summary>
/// Adamantium UI markup language parser
/// </summary>
public class AumlParser
{
    public static string MarupExtensionClassFullName => "Adamantium.UI.Core.MarkupExtensions.MarkupExtension";
    
    public static AumlDocument Load(string path)
    {
        var content = File.ReadAllText(path);
        return Parse(content);
    }
    
    public static AumlDocument Parse(string aumlString)
    {
        var root = XDocument.Parse(aumlString, LoadOptions.SetLineInfo).Root;

        var context = new ParserContext(root);
        var rootNode = context.Parse();

        if (!rootNode.TypeReference.ContainsValidData())
        {
            context.Logger.Error($"Xmlns declaration is missing for root element: {rootNode.TypeReference.Name}");

            return new AumlDocument()
            {
                Logger = context.Logger,
                HasErrors = true,
                Root = rootNode, // keep the AST; consumers gate on HasErrors, tooling can still inspect it
                NamespaceMappings = context.NamespaceMappings.ToArray(),
            };
        }

        var doc = new AumlDocument()
        {
            Logger = context.Logger,
            HasErrors = context.HasErrors,
            Root = rootNode,
            NamespaceMappings = context.NamespaceMappings.ToArray(),
        };
        
        return doc;
    }
}

public class ParserContext
{
    private readonly XElement rootElement;

    public Logger Logger { get; }
    
    public bool HasErrors => Logger.HasErrors;
    
    public List<NamespaceMapping> NamespaceMappings { get; }
    
    public ParserContext(XElement root)
    {
        Logger = new Logger();
        rootElement = root;
        NamespaceMappings = [];
    }

    /// <summary>The document's default xmlns - the one declared without a prefix, which is where an unqualified name
    /// resolves. Null before the root's declarations have been read, and the caller then falls back.</summary>
    private string DefaultNamespace()
        => NamespaceMappings.FirstOrDefault(m => string.IsNullOrEmpty(m.Prefix))?.Namespace;

    public AumlAstObjectNode Parse()
    {
        return ParseAumlNode(rootElement, true);
    }

    private AumlAstObjectNode ParseAumlNode(XElement element, bool isRoot)
    {
        var targetType = element.GetTypeReference();

        AumlAstObjectNode objectNode = null;
        switch (targetType.Name)
        {
            case "ControlTemplate":
            case "DataTemplate":
            case "HierarchicalDataTemplate":
                var templateNode = new AumlAstTemplateNode(element.ToLineInfo(), targetType, element.ToString());
                objectNode = templateNode;
                break;
            default:
                objectNode = new AumlAstObjectNode(element.ToLineInfo(), targetType);
                break;
        }

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                if (!isRoot) throw new Exception("xmlns could be defined only at the root element");

                var mapping = attribute.GetNamespaceMapping();
                NamespaceMappings.Add(mapping);
            }
            else // parse property or event
            {
                var propName = attribute.Name.LocalName;
                var ns = attribute.Name.Namespace.ToString();
                var ownerType = objectNode.TypeReference;
                bool isAttachedProperty = false;
                if (ns == AumlNamespaces.AumlDirective)
                {
                    objectNode.Children.Add(new AumlAstDirective(attribute.ToLineInfo(), objectNode, ns, propName,
                        new AumlAstTextNode(attribute.ToLineInfo(), attribute.Value)));
                    continue;
                }

                // check does we faced with using of attached property or attached event
                if (propName.Contains('.'))
                {
                    isAttachedProperty = true;
                    var names = propName.Split('.');
                    propName = names[1];

                    // The owner is named by the ATTRIBUTE's prefix, not the element's: nav:RegionManager.RegionName on a
                    // docking:DockingArea means the RegionManager of the nav namespace. Taking the element's namespace
                    // made an attached property resolvable only on elements that happened to live in the same namespace
                    // as its owner.
                    //
                    // UNPREFIXED (Canvas.Left="40") resolves in the DEFAULT xmlns, not in the element's. In XML an
                    // unqualified attribute carries no namespace at all and certainly does not inherit its element's, and
                    // standing the element's in only appeared to work because elements usually come from the same default
                    // xmlns as the owner. It breaks the moment they do not: Canvas.Left on a control declared with its own
                    // prefix went looking for a Canvas in THAT assembly, which has none.
                    var ownerNamespace = string.IsNullOrEmpty(ns) ? DefaultNamespace() ?? targetType.Namespace : ns;
                    ownerType = new AumlAstXmlTypeReference(element.ToLineInfo(), ownerNamespace, names[0]);
                }

                var propertyNode = new AumlAstPropertyNode(element.ToLineInfo(), 
                    new AumlAstPropertyReference(attribute.ToLineInfo(), isAttachedProperty, objectNode, ownerType, targetType, propName),
                    ParseTextOrMarkupExtension(attribute.Value, element, attribute.ToLineInfo()));
                objectNode.Children.Add(propertyNode);
            }
        }

        foreach (var node in element.Nodes())
        {
            if (node is XElement elNode && elNode.Name.LocalName.Contains('.'))
            {
                var names = elNode.Name.LocalName.Split('.');
                // A property-element whose owner prefix differs from the element's own type is an ATTACHED property in
                // property-element position: <StackPanel><ResourceContext.Resources>... The attribute path already sets
                // this (propName.Contains('.')); the property-element path used to hardcode false, so an attached property
                // written with child content resolved its member against the wrong type (the element) and failed.
                var isAttachedProperty = names[0] != objectNode.TypeReference?.Name;
                var property = new AumlAstPropertyNode(
                    elNode.ToLineInfo(),
                    new AumlAstPropertyReference(
                        elNode.ToLineInfo(),
                        isAttachedProperty,
                        objectNode,
                        new AumlAstXmlTypeReference(elNode.ToLineInfo(), elNode.Name.NamespaceName, names[0]),
                        targetType,
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
            return ParseMarkupExtension(extension, info);
        }

        return new AumlAstTextNode(info, extension);
    }
    
    private IAumlAstValueNode ParseMarkupExtension(string input, IAumlLineInfo info)
    {
        var result = MarkupExtensionParser.Parse(this, input, info, NamespaceMappings);
        return result;
    }
}

public static class AumlNamespaces
{
    public static string AumlDirective => "http://adamantium/ui/xaml/extensions";

    public static string AumlControls => "http://adamantium/ui";
    public static string AumlResources => "http://adamantium/ui/resources";
}