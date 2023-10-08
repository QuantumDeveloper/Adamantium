using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Markup;

namespace Adamantium.UI.Templates;

public static class AumlDocumentExtension
{
    public static AumlTemplateContainer TransformAumlDocument(this AumlDocument document)
    {
        var metaContainer = new AumlTemplateContainer();
        var stack = new Stack<IAumlAstNode>();
        stack.Push(document.Root);
        var allTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .ToList();

        while (stack.Count > 0)
        {
            var element = stack.Pop() as AumlAstObjectNode;

            if (string.IsNullOrEmpty(element.Type.Namespace))
            {
                var types = allTypes.Where(t => t.Name == element.Type.Name);
                var finalType = types.FirstOrDefault(x => x.Assembly.FullName.StartsWith("Adamantium.UI"));
                var clrType = new AumlAstClrTypeReference(element.GetLineInfo(), finalType.Namespace, finalType.Name, finalType.Assembly.FullName);
                element.Type = clrType;
                metaContainer.TypesMap[finalType.FullName] = finalType;
            }
            else if (!metaContainer.TypesMap.TryGetValue(element.Type.Namespace, out var controlType))
            {
                controlType = Type.GetType(element.Type.GetFullTypeName());
                metaContainer.TypesMap[element.Type.Namespace] = controlType;
            }

            foreach (var child in element.Children)
            {
                if (child is AumlAstObjectNode)
                {
                    stack.Push(child);
                }
                else if (child is AumlAstPropertyNode property)
                {
                    if (property.Property is AumlAstPropertyReference propertyReference)
                    {
                        if (propertyReference.Name == "Name")
                        {
                            var textNode = property.Values[0] as AumlAstTextNode;
                            metaContainer.NamedElements.Add(new NamedElement(textNode.Text, element));
                        }

                        foreach (var value in property.Values)
                        {
                            if (value is AumlAstObjectNode)
                            {
                                stack.Push(value);
                            }
                        }
                    }
                }
                else if (child is AumlAstDirective directive)
                {
                    if (directive.Name == "Name")
                    {
                        var textNode = directive.Value as AumlAstTextNode;
                        metaContainer.NamedElements.Add(new NamedElement(textNode.Text, element));
                    }
                }
            }
        }

        foreach (var named in metaContainer.NamedElements)
        {
            metaContainer.NamedElementsMap.Add(named.Element, named.Name);
        }

        metaContainer.RootNode = document.Root;

        return metaContainer;
    }
}