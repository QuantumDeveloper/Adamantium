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

   /// <summary>
   /// ALL visual elements under <paramref name="p"/>, front-to-back - INCLUDING non-input visuals (a Border, a Shape, a
   /// TextBlock). Mouse routing must NOT target those (a Border should let clicks fall through to interactive content),
   /// so it uses <see cref="GetInputElementsAt"/>; the DESIGNER, however, must be able to select ANY authored element,
   /// not only interactive ones - that is what this is for.
   /// </summary>
   public static IEnumerable<IUIComponent> GetVisualsAt(this IUIComponent root, Vector2 p)
   {
      var result = new List<IUIComponent>();
      CollectVisuals(root, p, result);
      return result;
   }

   private static void CollectVisuals(IUIComponent element, Vector2 p, List<IUIComponent> result)
   {
      if (!element.ClipRectangle.Contains(p)
          || element.Visibility != Visibility.Visible
          || !element.IsHitTestVisible)
         return;

      var local = p - element.ClipRectangle.Location;
      foreach (var child in ZSort(element.VisualChildren))
         CollectVisuals(child, local, result);

      // Any visual on its actual geometry is a candidate (not only IInputComponent) - the only difference from the input
      // collector above, so non-interactive authored elements are reachable by the designer's selection.
      if (element.HitTestCore(local))
         result.Add(element);
   }

   private static void Collect(IUIComponent element, Vector2 p, List<IInputComponent> result)
   {
      if (!element.ClipRectangle.Contains(p)
          || element.Visibility != Visibility.Visible
          || !element.IsEnabled
          || !element.IsHitTestVisible)
         return;

      // Into the element's local space, then recurse into ALL visual children front-to-back (not only input ones), so a
      // NON-input container - a Border / any Decorator - is descended THROUGH and the interactive content it wraps stays
      // reachable. Filtering to IInputComponent here made anything inside a Border dead to the mouse (e.g. a ScrollBar
      // whose template root is a Border: its Track/Thumb were unhittable).
      var local = p - element.ClipRectangle.Location;
      foreach (var child in ZSort(element.VisualChildren))
         Collect(child, local, result);

      // Narrow phase: only an INPUT element is a hit target (non-input visuals are pure pass-through containers), and
      // only if the point is on its actual geometry, not just inside its bounding box - so a click in a shape's empty
      // bbox corner falls through to whatever is really there.
      if (element is IInputComponent input && element.HitTestCore(local))
         result.Add(input);
   }

   // Front-to-back paint order: higher ZIndex first, then later siblings (higher index) first.
   private static IEnumerable<IUIComponent> ZSort(IEnumerable<IUIComponent> elements)
   {
      return elements
         .Select((element, index) => (element, index))
         .OrderByDescending(x => x.element.ZIndex)
         .ThenByDescending(x => x.index)
         .Select(x => x.element);
   }
}
