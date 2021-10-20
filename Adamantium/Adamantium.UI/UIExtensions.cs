using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI
{
   public static class UIExtensions
   {
      /// <summary>
      /// Enumerates the ancestors of an <see cref="IUIComponent"/> in the visual tree.
      /// </summary>
      /// <param name="visualComponent</param>
      /// <returns>The visual's ancestors.</returns>
      public static IEnumerable<IUIComponent> GetVisualAncestors(this IUIComponent visualComponent)
      {
         visualComponent = visualComponent.VisualParent;

         while (visualComponent != null)
         {
            yield return visualComponent;
            visualComponent = visualComponent.VisualParent;
         }
      }


      public static IEnumerable<IUIComponent> GetSelfAndVisualAncestors(this IUIComponent visualComponent)
      {
         yield return visualComponent;

         foreach (var ancestor in visualComponent.GetVisualAncestors())
         {
            yield return ancestor;
         }
      }

      public static T GetVisualParent<T>(this IUIComponent visualComponent) where T : class
      {
         return visualComponent.VisualParent as T;
      }

      public static Point PointToClient(this IUIComponent visualComponent, Point point)
      {
         var pair = GetRootAndAbsolutePosition(visualComponent);
         return pair.Key.PointToClient(point + pair.Value);
      }

      public static Point PointToScreen(this IUIComponent visualComponent, Point point)
      {
         var pair = GetRootAndAbsolutePosition(visualComponent);
         return pair.Key.PointToScreen(point +pair.Value);
      }

      private static KeyValuePair<IRootVisualComponent, Point> GetRootAndAbsolutePosition(this IUIComponent visualComponent)
      {
         Point p = new Point();

         while (!(visualComponent is IRootVisualComponent))
         {
            p += visualComponent.ClipRectangle.Location;

            visualComponent = visualComponent.VisualParent;

            if (visualComponent == null)
            {
               throw new InvalidOperationException("Control is not attached to visual tree.");
            }
         }

         return new KeyValuePair<IRootVisualComponent, Point>((IRootVisualComponent)visualComponent, p);
      } 
   }
}
