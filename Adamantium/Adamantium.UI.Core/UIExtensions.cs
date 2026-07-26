using Adamantium.Mathematics;

namespace Adamantium.UI.Core;

public static class UIExtensions
{
   /// <summary>
   /// Enumerates the ancestors of an <see cref="IUIComponent"/> in the visual tree.
   /// </summary>
   /// <param name="visualComponent"></param>
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

   // The logical parent, BRIDGING a template boundary via TemplatedParent. A template part has no LogicalParent (parts are
   // attached visual-only - see TemplatedUIComponent.AddTemplateChild), so a raw LogicalParent walk dead-ends at every
   // templated control. Hopping to TemplatedParent (set on every part by ControlTemplate.Build) crosses that island back
   // to its host - the WPF LogicalTreeHelper model. This is what lets {Ancestor ..., Logical=True} reach an ItemsControl
   // from inside a generated item's content, and gives resource lookup a continuous logical chain. See
   // docs/TREE_MODEL_DESIGN.md.
   public static IFundamentalUIComponent GetLogicalParentOrBridge(this IFundamentalUIComponent component)
      => component.LogicalParent ?? component.TemplatedParent as IFundamentalUIComponent;

   /// <summary>Enumerates the logical ancestors of a component, bridging template boundaries via TemplatedParent.</summary>
   public static IEnumerable<IFundamentalUIComponent> GetLogicalAncestors(this IFundamentalUIComponent component)
   {
      component = component.GetLogicalParentOrBridge();

      while (component != null)
      {
         yield return component;
         component = component.GetLogicalParentOrBridge();
      }
   }

   public static IEnumerable<IFundamentalUIComponent> GetSelfAndLogicalAncestors(this IFundamentalUIComponent component)
   {
      yield return component;

      foreach (var ancestor in component.GetLogicalAncestors())
      {
         yield return ancestor;
      }
   }

   public static Vector2 PointToClient(this IUIComponent visualComponent, Vector2 point)
   {
      var pair = GetRootAndAbsolutePosition(visualComponent);
      return pair.Key.PointToClient(point + pair.Value);
   }

   public static Vector2 PointToScreen(this IUIComponent visualComponent, Vector2 point)
   {
      var pair = GetRootAndAbsolutePosition(visualComponent);
      return pair.Key.PointToScreen(point + pair.Value);
   }

   private static KeyValuePair<IRootVisualComponent, Vector2> GetRootAndAbsolutePosition(this IUIComponent visualComponent)
   {
      Vector2 p = new Vector2();

      while (!(visualComponent is IRootVisualComponent))
      {
         p += visualComponent.ClipRectangle.Location;

         visualComponent = visualComponent.VisualParent;

         if (visualComponent == null)
         {
            throw new InvalidOperationException("Control is not attached to visual tree.");
         }
      }

      return new KeyValuePair<IRootVisualComponent, Vector2>((IRootVisualComponent)visualComponent, p);
   } 
}