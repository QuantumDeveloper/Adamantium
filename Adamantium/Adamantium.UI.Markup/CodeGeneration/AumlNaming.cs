using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.Markup.CodeGeneration;

public static class AumlNaming
{
    /// <summary>
    /// The CLR namespace a generated control class is emitted under: an explicit <c>x:Namespace</c> directive on the
    /// root (the WPF <c>x:Class</c> analog) wins; otherwise the assembly name plus the file's directory path. Shared by
    /// the pre-registration pass and the source generator so a cross-referenced view resolves to the EXACT name it is
    /// generated under.
    /// </summary>
    public static string ComputeNamespace(string relativeFilePath, string assemblyName, IAumlAstNode rootNode, string className)
    {
        var namespaceDirective = (rootNode as AumlAstObjectNode)?.Children
            .OfType<AumlAstDirective>()
            .FirstOrDefault(d => d.Name == AumlDirectives.Namespace);

        if (namespaceDirective?.Value is AumlAstTextNode directiveValue && !string.IsNullOrEmpty(directiveValue.Text))
        {
            // The value is a full type name "Namespace.ClassName" (like x:Class), or already a bare namespace.
            var value = directiveValue.Text;
            return value.EndsWith($".{className}")
                ? value.Substring(0, value.Length - className.Length - 1)
                : value;
        }

        var additionalPath = Path.GetDirectoryName(relativeFilePath);
        var @namespace = assemblyName;
        if (!string.IsNullOrEmpty(additionalPath))
        {
            @namespace = $"{@namespace}.{additionalPath.ToNamespace()}";
        }

        return @namespace;
    }
}
