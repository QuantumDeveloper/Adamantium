using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>Picks how one command is drawn in the quick-access bar: its OWN compact form when it brought one, else
/// <see cref="Default"/> - the icon button most commands are.
/// <para>Why a selector and not two templates: <c>ItemTemplate</c> wins over a selector in the presenter, so a bar that
/// set both would never ask. This owns the whole decision instead.</para></summary>
public class RibbonQuickAccessTemplateSelector : DataTemplateSelector
{
    public DataTemplate Default { get; set; }

    public override DataTemplate SelectTemplate(object item, AdamantiumComponent container)
        => (item as IQuickAccessItem)?.QuickAccessTemplate ?? Default;
}
