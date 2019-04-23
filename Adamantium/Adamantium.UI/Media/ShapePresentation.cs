using System;
using System.Collections;
using System.Collections.Generic;

namespace Adamantium.UI.Media
{
   internal class ShapePresentation:IEnumerable<PresentationItem>
   {
      private List<PresentationItem> items;

      public ShapePresentation()
      {
         items = new List<PresentationItem>();
      }

      public Boolean IsSealed { get; set; }

      public void AddItem(PresentationItem item)
      {
         if (!IsSealed)
         {
            items.Add(item);
         }
      }

      public void RemoveItem(PresentationItem item)
      {
         items.Remove(item);
      }

      public void DisposeAndClearItems()
      {
         for (int i = 0; i < items.Count; i++)
         {
            items[i].Dispose();
         }
         items.Clear();
      }

      public IEnumerator<PresentationItem> GetEnumerator()
      {
         return items.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return GetEnumerator();
      }

      public PresentationItem this [int i]
      {
         get { return items[i]; }
         set { items[i] = value; }
      }
   }
}
