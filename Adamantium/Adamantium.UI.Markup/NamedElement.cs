
namespace Adamantium.UI.Markup
{
    public class NamedElement
    {
        public NamedElement(string name, AumlAstObjectNode element)
        {
            Name = name;
            Element = element;
        }

        public string Name { get; set; }

        public AumlAstObjectNode Element { get; set; }
    }
}