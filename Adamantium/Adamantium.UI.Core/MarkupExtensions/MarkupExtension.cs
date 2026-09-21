using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.MarkupExtensions;

public abstract class MarkupExtension
{
    public abstract object ProvideObject(MarkupContext context);
}