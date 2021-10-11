using System.Collections.Generic;
using Adamantium.Core.Collections;

namespace Adamantium.UI
{
   public sealed class UIElementCollection:TrackingCollection<UiComponent>
   {
      public UIElementCollection() { }
      public UIElementCollection(IEnumerable<UiComponent> elements ):base(elements)
      { }
   }
}
