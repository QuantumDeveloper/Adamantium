using Adamantium.Core.Collections;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Controls.TypeParsers;

namespace Adamantium.UI.Controls.Panels;

[TypeParser(typeof(ColumDefinitionsParser))]
public class ColumnDefinitions:TrackingCollection<ColumnDefinition>
{
   public ColumnDefinitions()
   {
         
   }

   protected override void InsertItem(int index, ColumnDefinition item)
   {
      ArgumentNullException.ThrowIfNull(item);
      base.InsertItem(index, item);
   }
}