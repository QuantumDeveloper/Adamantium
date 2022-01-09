using System.Collections.Generic;
using Adamantium.Core.Collections;

namespace Adamantium.UI;

public sealed class UIElementCollection:TrackingCollection<UIComponent>
{
   public UIElementCollection() { }
   public UIElementCollection(IEnumerable<UIComponent> elements ):base(elements)
   { }
}