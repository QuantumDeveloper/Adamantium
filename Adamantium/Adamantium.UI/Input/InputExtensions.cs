using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;

namespace Adamantium.UI.Input
{
   public static class InputExtensions
   {
      public static IInputElement HitTest(this IInputElement root, Point p)
      {
         return root.GetInputElementsAt(p).FirstOrDefault();
      }

      public static IEnumerable<IInputElement> GetInputElementsAt(this IInputElement root, Point p)
      {
         
         List<IInputElement> elements = new List<IInputElement>();
         Stack<IInputElement> stack = new Stack<IInputElement>();

         stack.Push((UiComponent)root);

         while (stack.Count > 0)
         {
            var current = stack.Pop();

            if (
               current.ClipRectangle.Contains(p) &&
               current.Visibility == Visibility.Visible &&
               current.IsEnabled &&
               current.IsHitTestVisible)
            {
               p -= current.ClipRectangle.Location;
               
               if (current.VisualChildren.Any())
               {
                  foreach (var child in ZSort(current.VisualChildren.OfType<IInputElement>().Reverse()))
                  {
                     if (
                        child.ClipRectangle.Contains(p) &&
                        child.Visibility == Visibility.Visible &&
                        child.IsEnabled &&
                        child.IsHitTestVisible)
                     {
                        stack.Push((UiComponent) child);
                     }
                  }
               }
               elements.Add(current);
            }
         }
         
         elements.Reverse();
         return elements;
         
         
         /*
         if (
            root.ClipRectangle.Contains(p) &&
            root.Visibility == Visibility.Visible &&
            root.IsEnabled &&
            root.IsHitTestVisible)
         {
            p -= root.ClipRectangle.Location;
            if (root.VisualChildren.Any())
            {
               foreach (var child in ZSort(root.VisualChildren.OfType<IInputElement>()))
               {
                  foreach (var result in child.GetInputElementsAt(p))
                  {
                     yield return result;
                  }
               }
            }

            yield return root;
         }
         */
      }

      private static IEnumerable<IInputElement> ZSort(IEnumerable<IInputElement> elements)
      {
         return elements.Select((element, index) => new ZOrderedElement
         {
            Element = element,
            Index = index,
            ZIndex = element.ZIndex
         }).OrderBy(x => x, null).Select(x => x.Element);
      }

      private class ZOrderedElement : IComparable<ZOrderedElement>
      {
         public IInputElement Element { get; set; }
         public int Index { get; set; }
         public int ZIndex { get; set; }

         public int CompareTo(ZOrderedElement other)
         {
            var z = other.ZIndex - ZIndex;

            if (z != 0)
            {
               return z;
            }
            else
            {
               return other.Index - Index;
            }
         }
      }
   }
}
