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

      public static Vector2D Vector2DToClient(this IUIComponent visualComponent, Vector2D Vector2D)
      {
         var pair = GetRootAndAbsolutePosition(visualComponent);
         return pair.Key.Vector2DToClient(Vector2D + pair.Value);
      }

      public static Vector2D Vector2DToScreen(this IUIComponent visualComponent, Vector2D Vector2D)
      {
         var pair = GetRootAndAbsolutePosition(visualComponent);
         return pair.Key.Vector2DToScreen(Vector2D +pair.Value);
      }

      private static KeyValuePair<IRootVisualComponent, Vector2D> GetRootAndAbsolutePosition(this IUIComponent visualComponent)
      {
         Vector2D p = new Vector2D();

         while (!(visualComponent is IRootVisualComponent))
         {
            p += visualComponent.ClipRectangle.Location;

            visualComponent = visualComponent.VisualParent;

            if (visualComponent == null)
            {
               throw new InvalidOperationException("Control is not attached to visual tree.");
            }
         }

         return new KeyValuePair<IRootVisualComponent, Vector2D>((IRootVisualComponent)visualComponent, p);
      } 
   }
}
