using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.MarkupExtensions;

public class ResourceLink : MarkupExtension
{
    public Type Source { get; set; }
    
    public ResourceScope Scope { get; set; }
    
    public override object ProvideObject(MarkupContext context)
    {
        return this;
    }
}