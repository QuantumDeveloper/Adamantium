using System.Collections.Generic;
using Adamantium.Core.Collections;

namespace Adamantium.UI;

public sealed class UIElementCollection : TrackingCollection<IUIComponent>
{
    public UIElementCollection()
    {
    }

    public UIElementCollection(IEnumerable<IUIComponent> elements) : base(elements)
    {
    }
}