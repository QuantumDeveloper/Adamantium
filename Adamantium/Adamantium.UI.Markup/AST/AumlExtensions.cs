using Adamantium.UI.Markup.AST.MarkupExtension;

namespace Adamantium.UI.Markup.AST
{
    public static class AumlExtensions
    {
        public static IEnumerable<AumlAstPropertyNode> GetProperties(this IAumlAstNode node)
        {
            if (node is AumlAstObjectNode objectNode)
            {
                var properties = objectNode.Children.Where(x => x is AumlAstPropertyNode).Cast<AumlAstPropertyNode>().ToList();
                return properties;
            }

            return Array.Empty<AumlAstPropertyNode>();
        }

        public static IEnumerable<AumlAstObjectNode> GetLogicalChildrenObjects(this IAumlAstNode node)
        {
            if (node is AumlAstObjectNode objectNode)
            {
                var properties = objectNode.Children.Where(x => x is AumlAstObjectNode).Cast<AumlAstObjectNode>().ToList();
                return properties;
            }

            return Array.Empty<AumlAstObjectNode>();
        }

        public static IEnumerable<AumlAstPropertyNode> GetResourceDictionaries(this IAumlAstNode node)
        {
            var properties = new List<AumlAstPropertyNode>();
            if (node is AumlAstObjectNode objectNode)
            {
                var stack = new Stack<IAumlAstNode>();
                stack.Push(objectNode);
                while(stack.Count > 0)
                {
                    var current = stack.Pop();

                    if (current is AumlAstPropertyNode propertyNode)
                    {
                        properties.Add(propertyNode);

                        foreach(var value in propertyNode.Values)
                        {
                            stack.Push(value);
                        }
                    }

                    if (current is AumlAstObjectNode objNode)
                    {
                        foreach(var value in objNode.Children)
                        {
                            stack.Push(value);
                        }
                    }
                }

                var resourceDictionaries = properties.Where(x => x.Property is AumlAstPropertyReference prop && prop.Name == "Source").ToList();
                
                return resourceDictionaries;
            }

            return [];
        }

        public static bool IsTextNode(this IAumlAstValueNode valueNode)
        {
            return valueNode is AumlAstTextNode;
        }

        public static string GetTextValue(this IAumlAstValueNode valueNode)
        {
            return valueNode switch
            {
                AumlAstTextNode textNode => textNode.Text,
                _ => string.Empty
            };
        }

        public static int GetPropertyValuesCount(this IAumlAstNode node)
        {
            if (node is AumlAstPropertyNode property)
            {
                return property.Values.Count;
            }

            return 0;
        }

        public static string GetTextValue(this AumlAstPropertyNode property, int index = 0)
        {
            return property.Values[index].ToString();
        }

        public static bool IsSetter(this AumlAstPropertyNode property)
        {
            return property.Property is AumlAstPropertyReference propertyReference &&
                   propertyReference.OwnerType.Name == "Setter";
        }
    }
}
