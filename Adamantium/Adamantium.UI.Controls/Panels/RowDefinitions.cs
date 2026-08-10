using Adamantium.Core.Collections;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core;
using Adamantium.UI.Controls.TypeParsers;

namespace Adamantium.UI.Controls.Panels;

[TypeParser(typeof(RowDefinitionsParser))]
[MarkupItem(ItemType = typeof(RowDefinition), ItemProperty = nameof(RowDefinition.Height))]
public class RowDefinitions: TrackingCollection<RowDefinition>
{
   public RowDefinitions()
   {
         
   }

   protected override void InsertItem(int index, RowDefinition item)
   {
      ArgumentNullException.ThrowIfNull(item);
      base.InsertItem(index, item);
   }
}