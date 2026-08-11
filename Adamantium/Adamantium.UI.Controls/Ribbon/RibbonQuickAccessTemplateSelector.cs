using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>The bar's DEFAULT choice: a command's own compact form when it brought one, else <see cref="Default"/> -
/// the icon button most commands are.
/// <para>Only a default. Which visual a command gets is the APPLICATION's decision, and it makes it by putting its own
/// selector on <c>RibbonQuickAccess.ItemTemplateSelector</c>: the items in the bar are the application's own type, so
/// only it can switch on what they are - a toggle, a chooser, whatever its command model has. The engine deliberately
/// does not grow a state to carry across for that; a visual would be owning what it only displays, and no fixed set of
/// kinds would fit every application anyway.</para>
/// <para>Why a selector and not two templates: <c>ItemTemplate</c> wins over a selector in the presenter, so a bar that
/// set both would never ask. This owns the whole decision instead.</para></summary>
public class RibbonQuickAccessTemplateSelector : DataTemplateSelector
{
    /// <summary>The plain case, when the author of the selector states one. Left unset, the bar's own
    /// <see cref="RibbonQuickAccess.DefaultItemTemplate"/> is used - which is where the THEME puts the icon button, so an
    /// application that derives from this to add a case of its own does not have to re-draw the ordinary one.</summary>
    public DataTemplate Default { get; set; }

    public override DataTemplate SelectTemplate(object item, AdamantiumComponent container)
        => (item as IQuickAccessItem)?.QuickAccessTemplate ?? Default ?? FromTheBar(container);

    private static DataTemplate FromTheBar(AdamantiumComponent container)
        => (container as IUIComponent)?.GetVisualAncestors().OfType<RibbonQuickAccess>().FirstOrDefault()?.DefaultItemTemplate;
}
