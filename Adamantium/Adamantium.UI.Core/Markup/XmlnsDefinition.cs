namespace Adamantium.UI.Core.Markup;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class XmlnsDefinitionAttribute : Attribute
{
    public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(xmlNamespace);
        ArgumentNullException.ThrowIfNull(clrNamespace);

        if (!clrNamespace.Contains("clr-namespace:") && !clrNamespace.Contains("assembly:") && !clrNamespace.Contains(';'))
            throw new ArgumentException($"{nameof(clrNamespace)} is in incorrect format");

        // Accept both "assembly=Name" and "assembly:Name" - the markup declarations use '=', the old code
        // split only on ':' and threw IndexOutOfRange when instantiated at runtime.
        var parts = clrNamespace.Split(';');
        ClrNamespace = ExtractValue(parts[0]);
        Assembly = parts.Length > 1 ? ExtractValue(parts[1]) : string.Empty;

        XmlNamespace = xmlNamespace;
        Prefix = prefix;
    }

    private static string ExtractValue(string part)
    {
        var separator = part.IndexOfAny(new[] { '=', ':' });
        return separator >= 0 ? part.Substring(separator + 1).Trim() : part.Trim();
    }
    
    public string XmlNamespace { get; }
    
    public string ClrNamespace { get; }
    
    public string Assembly { get; }
    
    public string Prefix { get; }
}