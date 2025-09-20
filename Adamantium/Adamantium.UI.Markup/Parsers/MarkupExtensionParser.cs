using System.Text;
using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.AST.MarkupExtension;
using Adamantium.UI.Markup.AST.TypeReference;

namespace Adamantium.UI.Markup.Parsers;

public class MarkupExtensionParser
{
    public static IAumlAstValueNode Parse(ParserContext context, string markup, IAumlLineInfo info, List<NamespaceMapping> namespaceMappings)
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

        var extensionTypeRef = ParseTypeName(context, typeName, info, namespaceMappings);

        if (extensionTypeRef is AumlAstXmlTypeReference xmlTypeRef
            && xmlTypeRef.Namespace == AumlNamespaces.AumlDirective)
        {
            var directive = new AumlAstDirective(
                info,
                null,
                xmlTypeRef.Namespace,
                xmlTypeRef.Name,
                new AumlAstTextNode(info, body))
            {
                TypeReference = extensionTypeRef,
            };
            return directive;
        }

        var result = new AumlAstMarkupExtensionNode(info, extensionTypeRef);
        var splitedParts = SplitByCommasRespectingBraces(body);

        foreach (var part in splitedParts)
        {
            int eq = part.IndexOf('=');
            if (eq >= 0)
            {
                string key = part.Substring(0, eq).Trim();
                string value = part.Substring(eq + 1).Trim();
                var res = ParseValue(context, value, info, namespaceMappings);
                result.Arguments.Add(new MarkupArgument(info, key, res));
            }
            else if (!string.IsNullOrWhiteSpace(part))
            {
                var res = ParseValue(context, part.Trim(), info, namespaceMappings);
                result.Arguments.Add(new MarkupArgument(info, string.Empty, res));
            }
        }

        return result;
    }

    public static IAumlAstTypeReference ParseTypeName(ParserContext context, string name, IAumlLineInfo info,
        List<NamespaceMapping> namespaceMappings)
    {
        name = name.Trim();
        if (name.Contains(":"))
        {
            var parts = name.Split(':');
            var prefix = parts[0];
            var typeName = parts[1];
            var mapping = namespaceMappings.FirstOrDefault(x => x.Prefix == prefix);
            if (mapping == null)
            {
                context.Logger.Error($"Prefix {prefix} is not defined in root element.");
                // Log diagnostic error here
                return new AumlAstXmlTypeReference(info, string.Empty, typeName);
            }

            if (mapping.IsClrNamespace)
            {
                return new AumlAstClrTypeReference(info, mapping.Namespace, typeName, mapping.Assembly);
            }

            return new AumlAstXmlTypeReference(info, mapping.Namespace, typeName);
        }
        else
        {
            // If there is no prefix - this is type withpout namespace (can be in C#)

            return new AumlAstClrTypeReference(info, string.Empty, name, string.Empty);
        }
    }

    private static IAumlAstValueNode ParseValue(ParserContext context, string value, IAumlLineInfo info, List<NamespaceMapping> namespaceMappings)
    {
        if (value.StartsWith("{") && value.EndsWith("}"))
        {
            return Parse(context, value, info, namespaceMappings);
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