using Adamantium.UI.Core.MarkupExtensions;

namespace Adamantium.UI.Core.Resources;

public class ResourceReference : MarkupExtension
{
    public ResourceReference(string resourceName)
    {
        Name = resourceName;
    }
    public string Name { get; }
    public override object ProvideObject(MarkupContext context)
    {
        return this;
    }
}