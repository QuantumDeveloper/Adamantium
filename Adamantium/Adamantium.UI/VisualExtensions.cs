using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI
{
   public static class VisualExtensions
   {
      /// <summary>
      /// Enumerates the ancestors of an <see cref="IVisual"/> in the visual tree.
      /// </summary>
      /// <param name="visual">The visual.</param>
      /// <returns>The visual's ancestors.</returns>
      public static IEnumerable<IVisual> GetVisualAncestors(this IVisual visual)
      {
         visual = visual.VisualParent;

         while (visual != null)
         {
            yield return visual;
            visual = visual.VisualParent;
         }
      }


      public static IEnumerable<IVisual> GetSelfAndVisualAncestors(this IVisual visual)
      {
         yield return visual;

         foreach (var ancestor in visual.GetVisualAncestors())
         {
            yield return ancestor;
         }
      }

      public static T GetVisualParent<T>(this IVisual visual) where T : class
      {
         return visual.VisualParent as T;
      }

      public static IReadOnlyList<IVisual> GetVisualDescends(this IVisual visual)
      {
         return visual.VisualChildren;
      }

      public static Point PointToClient(this IVisual visual, Point point)
      {
         var pair = GetRootAndAbsolutePosition(visual);
         return pair.Key.PointToClient(point + pair.Value);
      }

      public static Point PointToScreen(this IVisual visual, Point point)
      {
         var pair = GetRootAndAbsolutePosition(visual);
         return pair.Key.PointToScreen(point +pair.Value);
      }

      private static KeyValuePair<IRootVisual, Point> GetRootAndAbsolutePosition(this IVisual visual)
      {
         Point p = new Point();

         while (!(visual is IRootVisual))
         {
            p += visual.ClipRectangle.Location;

            visual = visual.VisualParent;

            if (visual == null)
            {
               throw new InvalidOperationException("Control is not attached to visual tree.");
            }
         }

         return new KeyValuePair<IRootVisual, Point>((IRootVisual)visual, p);
      } 
   }
}
