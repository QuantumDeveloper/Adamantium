namespace Adamantium.UI.Xaml;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class XmlnsDefinitionAttribute : Attribute
{
    public XmlnsDefinitionAttribute(string xmlNamespace, string definition, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(xmlNamespace);
        ArgumentNullException.ThrowIfNull(definition);
        
        XmlNamespace = xmlNamespace;
        Definition = definition;
        Prefix = prefix;
    }
    
    public string XmlNamespace { get; }
    
    public string Definition { get; }
    
    public string Prefix { get; }
}