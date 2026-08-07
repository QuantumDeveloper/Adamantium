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

   /// <summary>A desktop point (physical) in this element's OWN logical coordinates: converted at the root, then back
   /// down by everything between it and the element.</summary>
   public static Vector2 PointToClient(this IUIComponent visualComponent, PixelPoint point)
   {
      var pair = GetRootAndAbsolutePosition(visualComponent);
      // MINUS: the root gives the point in ITS coordinates, and the element sits at pair.Value inside it. This used to
      // add - and to add the element's offset to the SCREEN point, before any conversion - which is the kind of mistake
      // that compiles while every value in the expression is a bare vector. It was invisible because every caller asks
      // this of a ROOT, where the offset is zero.
      return pair.Key.PointToClient(point) - pair.Value;
   }

   /// <summary>A point of this element's own logical coordinates as a desktop point (physical pixels).</summary>
   public static PixelPoint PointToScreen(this IUIComponent visualComponent, Vector2 point)
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