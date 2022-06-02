using System;

namespace Adamantium.UI.Markup;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class XmlnsDefinitionAttribute : Attribute
{
    public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(xmlNamespace);
        ArgumentNullException.ThrowIfNull(clrNamespace);

        if (!clrNamespace.Contains("clr-namespace:") && !clrNamespace.Contains("assembly:") && !clrNamespace.Contains(";"))
            throw new ArgumentException($"{nameof(clrNamespace)} is in incorrect format");

        var str = clrNamespace.Split(";");
        ClrNamespace = str[0].Split(":")[1];
        Assembly = str[1].Split(":")[1];
        
        XmlNamespace = xmlNamespace;
        Prefix = prefix;
    }
    
    public string XmlNamespace { get; }
    
    public string ClrNamespace { get; }
    
    public string Assembly { get; }
    
    public string Prefix { get; }
}