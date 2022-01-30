using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;

namespace Adamantium.UI.Input;

public static class InputExtensions
{
   public static IInputComponent HitTest(this IInputComponent root, Vector2 p)
   {
      return root.GetInputElementsAt(p).FirstOrDefault();
   }

   public static IEnumerable<IInputComponent> GetInputElementsAt(this IInputComponent root, Vector2 p)
   {
         
      List<IInputComponent> elements = new List<IInputComponent>();
      Stack<IInputComponent> stack = new Stack<IInputComponent>();

      stack.Push((UIComponent)root);

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
               foreach (var child in ZSort(current.VisualChildren.OfType<IInputComponent>().Reverse()))
               {
                  if (
                     child.ClipRectangle.Contains(p) &&
                     child.Visibility == Visibility.Visible &&
                     child.IsEnabled &&
                     child.IsHitTestVisible)
                  {
                     stack.Push((UIComponent) child);
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

   private static IEnumerable<IInputComponent> ZSort(IEnumerable<IInputComponent> elements)
   {
      return elements.Select((element, index) => new ZOrderedElement
      {
         Component = element,
         Index = index,
         ZIndex = element.ZIndex
      }).OrderBy(x => x, null).Select(x => x.Component);
   }

   private class ZOrderedElement : IComparable<ZOrderedElement>
   {
      public IInputComponent Component { get; set; }
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