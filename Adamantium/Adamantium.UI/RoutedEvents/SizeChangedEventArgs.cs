using System;

namespace Adamantium.UI
{
   public class SizeChangedEventArgs:RoutedEventArgs
   {
      public Size NewSize { get; private set; }
      public Size OldSize { get; private set; }
      public Boolean WidthChanged { get; private set; }
      public Boolean HeightChanged { get; private set; }

      public SizeChangedEventArgs(Size oldSize, Size newSize, bool widthChanged, bool heightChanged)
      {
         NewSize = newSize;
         OldSize = oldSize;
         WidthChanged = widthChanged;
         HeightChanged = heightChanged;
      }

   }
}
