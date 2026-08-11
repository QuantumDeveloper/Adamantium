using Adamantium.Core.Collections;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>The contexts a ribbon knows about, declared once on it. A tab names the one it belongs to by
/// <see cref="RibbonContextualGroup.Key"/>, because a group is not a visual and so cannot be reached by
/// <c>{Binding ElementName}</c> - that walk looks through the visual tree.</summary>
[MarkupItem(ItemType = typeof(RibbonContextualGroup), ItemProperty = nameof(RibbonContextualGroup.Key))]
public class RibbonContextualGroups : TrackingCollection<RibbonContextualGroup>
{
}
