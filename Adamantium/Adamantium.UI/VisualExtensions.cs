using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI
{
   public static class VisualExtensions
   {
      /// <summary>
      /// Enumerates the ancestors of an <see cref="IVisualComponent"/> in the visual tree.
      /// </summary>
      /// <param name="visualComponentual.</param>
      /// <returns>The visual's ancestors.</returns>
      public static IEnumerable<IVisualComponent> GetVisualAncestors(this IVisualComponent visualComponent)
      {
         visualComponent = visualComponent.VisualComponentParent;

         while (visualComponent != null)
         {
            yield return visualComponent;
            visualComponent = visualComponent.VisualComponentParent;
         }
      }


      public static IEnumerable<IVisualComponent> GetSelfAndVisualAncestors(this IVisualComponent visualComponent)
      {
         yield return visualComponent;

         foreach (var ancestor in visualComponent.GetVisualAncestors())
         {
            yield return ancestor;
         }
      }

      public static T GetVisualParent<T>(this IVisualComponent visualComponent) where T : class
      {
         return visualComponent.VisualComponentParent as T;
      }

      public static IReadOnlyList<IVisualComponent> GetVisualDescends(this IVisualComponent visualComponent)
      {
         return visualComponent.VisualChildren;
      }

      public static Point PointToClient(this IVisualComponent visualComponent, Point point)
      {
         var pair = GetRootAndAbsolutePosition(visualComponent);
         return pair.Key.PointToClient(point + pair.Value);
      }

      public static Point PointToScreen(this IVisualComponent visualComponent, Point point)
      {
         var pair = GetRootAndAbsolutePosition(visualComponent);
         return pair.Key.PointToScreen(point +pair.Value);
      }

      private static KeyValuePair<IRootVisualComponent, Point> GetRootAndAbsolutePosition(this IVisualComponent visualComponent)
      {
         Point p = new Point();

         while (!(visualComponent is IRootVisualComponent))
         {
            p += visualComponent.ClipRectangle.Location;

            visualComponent = visualComponent.VisualComponentParent;

            if (visualComponent == null)
            {
               throw new InvalidOperationException("Control is not attached to visual tree.");
            }
         }

         return new KeyValuePair<IRootVisualComponent, Point>((IRootVisualComponent)visualComponent, p);
      } 
   }
}
