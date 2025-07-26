using Adamantium.Core.Collections;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Controls.TypeParsers;

namespace Adamantium.UI.Controls.Panels;

[TypeParser(typeof(RowDefinitionsParser))]
public class RowDefinitions: TrackingCollection<RowDefinition>
{
   public RowDefinitions()
   {
         
   }
}