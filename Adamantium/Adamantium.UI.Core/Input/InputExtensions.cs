using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Input;

public static class InputExtensions
{
   public static IInputComponent HitTest(this IInputComponent root, Vector2 p)
   {
      return root.GetInputElementsAt(p).FirstOrDefault();
   }

   /// <summary>
   /// The input elements under <paramref name="p"/>, FRONT-TO-BACK: the visually top-most (last-painted / highest
   /// ZIndex) hit comes first, so <see cref="HitTest"/> returns it. Children paint over their parent and later
   /// children over earlier ones, so they are tested in reverse paint order; an element itself sits behind its
   /// children. Earlier this returned the BOTTOM-most overlapping sibling, so a control drawn on top of another (e.g.
   /// a Line over a Panel) couldn't be hit/selected.
   /// </summary>
   public static IEnumerable<IInputComponent> GetInputElementsAt(this IInputComponent root, Vector2 p)
   {
      var result = new List<IInputComponent>();
      Collect(root, p, result);
      return result;
   }

   private static void Collect(IInputComponent element, Vector2 p, List<IInputComponent> result)
   {
      if (!element.ClipRectangle.Contains(p)
          || element.Visibility != Visibility.Visible
          || !element.IsEnabled
          || !element.IsHitTestVisible)
         return;

      // Into the element's local space, then recurse into children front-to-back so the top-most hit is collected first.
      var local = p - element.ClipRectangle.Location;
      foreach (var child in ZSort(element.VisualChildren.OfType<IInputComponent>()))
         Collect(child, local, result);

      // Narrow phase: the element is hit (behind its children) only if the point is on its actual geometry, not just
      // inside its bounding box - so a click in a shape's empty bbox corner falls through to whatever is really there.
      if (element.HitTestCore(local))
         result.Add(element);
   }

   // Front-to-back paint order: higher ZIndex first, then later siblings (higher index) first.
   private static IEnumerable<IInputComponent> ZSort(IEnumerable<IInputComponent> elements)
   {
      return elements
         .Select((element, index) => (element, index))
         .OrderByDescending(x => x.element.ZIndex)
         .ThenByDescending(x => x.index)
         .Select(x => x.element);
   }
}
