using System.Text;
using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.AST.MarkupExtension;
using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.Parsers;

public class MarkupExtensionParser
{
    public static IAumlAstValueNode Parse(string markup, IAumlLineInfo info, List<NamespaceMapping> namespaceMappings)
    {
        markup = markup.Trim();
        
        if (!markup.StartsWith("{") || !markup.EndsWith("}"))
            throw new ArgumentException("Markup extension must be wrapped in { }");
        
        markup = markup.Substring(1, markup.Length - 2).Trim();
        
        int space = markup.IndexOf(' ');
        string typeName;
        string body;

        if (space < 0)
        {
            typeName = markup;
            body = string.Empty;
        }
        else
        {
            typeName = markup.Substring(0, space).Trim();
            body = markup.Substring(space + 1).Trim();
        }

        IAumlAstTypeReference typeRef = null;
        if (typeName.Contains(":"))
        {
            var parts = typeName.Split(':');
            var mapping = namespaceMappings.FirstOrDefault(x => x.Prefix == parts[0]);
            if (mapping.IsClrNamespace)
            {
                typeRef = new AumlAstClrTypeReference(info, mapping.Namespace, parts[1], mapping.Assembly);
            }
            else
            {
                typeRef = new AumlAstXmlTypeReference(info, mapping.Namespace, parts[1]);
            }
        }
        else
        {
            typeRef = new AumlAstClrTypeReference(info, string.Empty, typeName, string.Empty);
        }
        var result = new AumlAstMarkupExtensionNode(info, typeRef);

        var splitedParts = SplitByCommasRespectingBraces(body);

        foreach (var part in splitedParts)
        {
            int eq = part.IndexOf('=');
            if (eq >= 0)
            {
                string key = part.Substring(0, eq).Trim();
                string value = part.Substring(eq + 1).Trim();
                var res = ParseValue(value, info, namespaceMappings);
                result.Arguments.Add(new MarkupArgument(info, key, res));
            }
            else if (!string.IsNullOrWhiteSpace(part))
            {
                var res = ParseValue(part.Trim(), info, namespaceMappings);
                result.Arguments.Add(new MarkupArgument(info, string.Empty, res));
            }
        }

        return result;
    }
    
    private static IAumlAstValueNode ParseValue(string value, IAumlLineInfo info, List<NamespaceMapping> namespaceMappings)
    {
        if (value.StartsWith("{") && value.EndsWith("}"))
        {
            return Parse(value, info, namespaceMappings);
        }

        return new AumlAstMarkupExtensionLiteral(info, value);
    }
    
    private static List<string> SplitByCommasRespectingBraces(string input)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input)) return result;

        var sb = new StringBuilder();
        int depth = 0;

        foreach (char c in input)
        {
            if (c == '{') depth++;
            if (c == '}') depth--;
            if (c == ',' && depth == 0)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
            result.Add(sb.ToString().Trim());

        return result;
    }
}