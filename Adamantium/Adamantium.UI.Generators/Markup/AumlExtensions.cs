using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Adamantium.UI.Markup
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

        public static IEnumerable<AumlAstObjectNode> GetObjects(this IAumlAstNode node)
        {
            if (node is AumlAstObjectNode objectNode)
            {
                var properties = objectNode.Children.Where(x => x is AumlAstObjectNode).Cast<AumlAstObjectNode>().ToList();
                return properties;
            }

            return Array.Empty<AumlAstObjectNode>();
        }

        public static bool IsTextNode(this IAumlAstValueNode valueNode)
        {
            return valueNode is AumlAstTextNode;
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
            var textNode = property.Values[index] as AumlAstTextNode;
            return textNode.Text;
        }
    }
}
