using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Input;

public static class InputExtensions
{
   public static IInputComponent HitTest(this IUIComponent root, Vector2 p)
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
   public static IEnumerable<IInputComponent> GetInputElementsAt(this IUIComponent root, Vector2 p)
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
      if (element.Visibility != Visibility.Visible
          || !element.IsHitTestVisible)
         return;

      // Same broad-phase rule as Collect: the box gates the SELF hit, but recursion into children is only pruned when
      // the element actually clips them (ClipToBounds), so an overflowing (but rendered) child stays selectable.
      var inBox = element.ClipRectangle.Contains(p);
      if (element.ClipToBounds && !inBox)
         return;

      var local = p - element.ClipRectangle.Location;
      foreach (var child in HitTestChildren(element, local))
         CollectVisuals(child, local, result);

      // Any visual on its actual geometry is a candidate (not only IInputComponent) - the only difference from the input
      // collector above, so non-interactive authored elements are reachable by the designer's selection.
      if (inBox && element.HitTestCore(local))
         result.Add(element);
   }

   /// <summary>The top-most INPUT element whose BOUNDS contain <paramref name="p"/> - the narrow phase (a shape's real
   /// geometry, a panel's visible background) is skipped, so a transparent container still counts. The mouse-over
   /// FALLBACK when the pixel-accurate <see cref="HitTest"/> misses: the pointer over a gap between transparent tiles is
   /// still geometrically inside the scroll viewer, so the over-chain must stay anchored there instead of collapsing to
   /// the window root (which flickers the viewer's IsMouseOver and restarts its auto-hide fade every few frames).</summary>
   public static IInputComponent HitTestBounds(this IUIComponent root, Vector2 p)
   {
      var result = new List<IInputComponent>();
      Collect(root, p, result, boundsOnly: true);
      return result.FirstOrDefault();
   }

   private static void Collect(IUIComponent element, Vector2 p, List<IInputComponent> result, bool boundsOnly = false)
   {
      if (element.Visibility != Visibility.Visible
          || !element.IsEnabled
          || !element.IsHitTestVisible)
         return;

      // Broad phase. Whether the point is inside THIS element's own box gates the SELF hit below: the default narrow
      // phase (UIComponent.HitTestCore => true, Panel => Background.IsVisible()) ignores the point and trusts this, so
      // it must stay for self-hit or every filled element would register as hit everywhere.
      var inBox = element.ClipRectangle.Contains(p);

      // ...but it must NOT gate recursion into children when the element does not clip them. A non-clipping element
      // (ClipToBounds=false, the default) does not clip its children in RENDER, so a child may legitimately overflow
      // its parent's box and still be drawn - and therefore must stay hittable. Pruning recursion by the parent box
      // made such overflow dead to the mouse (e.g. a StackPanel squeezed a few px shorter than its rows by a tight
      // slot: the last row rendered but its lower part could not be clicked). A CLIPPING element (ClipToBounds=true)
      // does hide what leaves its box, in render and here, so it still prunes.
      if (element.ClipToBounds && !inBox)
         return;

      // Into the element's local space, then recurse into ALL visual children front-to-back (not only input ones), so a
      // NON-input container - a Border / any Decorator - is descended THROUGH and the interactive content it wraps stays
      // reachable. Filtering to IInputComponent here made anything inside a Border dead to the mouse (e.g. a ScrollBar
      // whose template root is a Border: its Track/Thumb were unhittable).
      var local = p - element.ClipRectangle.Location;
      foreach (var child in HitTestChildren(element, local))
         Collect(child, local, result, boundsOnly);

      // Narrow phase: only an INPUT element is a hit target (non-input visuals are pure pass-through containers), the
      // point must be inside its box (broad), and on its actual geometry, not just inside its bounding box - so a click
      // in a shape's empty bbox corner falls through to whatever is really there. boundsOnly skips the geometry test
      // (the mouse-over fallback: bounds containment is enough).
      if (inBox && element is IInputComponent input && (boundsOnly || element.HitTestCore(local)))
         result.Add(input);
   }

   // The children to recurse into for a hit-test, front-to-back. A container that can spatially resolve the point (a
   // virtualizing panel) returns just the candidate(s) - O(1) instead of walking thousands of tiles; else recurse ALL
   // visual children in paint order.
   private static IEnumerable<IUIComponent> HitTestChildren(IUIComponent element, Vector2 local)
   {
      if (element is IHitTestChildren provider)
      {
         var subset = provider.GetHitTestChildren(local);
         if (subset != null) return subset;
      }
      return ZSort(element.VisualChildren);
   }

   // Front-to-back paint order: higher ZIndex first, then later siblings (higher index) first. Fast path: when NO child
   // sets a non-zero ZIndex (the overwhelmingly common case) paint order IS child order, so just walk it in reverse with
   // ZERO allocation - the old unconditional LINQ Select+OrderBy+Select allocated several buffers PER visited node, which
   // over a deep hit-test of thousands of nodes per mouse move was a GC storm (the mouse-move freeze).
   private static IEnumerable<IUIComponent> ZSort(IEnumerable<IUIComponent> elements)
   {
      var list = elements as IReadOnlyList<IUIComponent> ?? elements.ToList();
      var anyZ = false;
      for (var i = 0; i < list.Count; i++)
         if (list[i].ZIndex != 0) { anyZ = true; break; }

      if (!anyZ)
         return ReverseOf(list);

      return list
         .Select((element, index) => (element, index))
         .OrderByDescending(x => x.element.ZIndex)
         .ThenByDescending(x => x.index)
         .Select(x => x.element);
   }

   private static IEnumerable<IUIComponent> ReverseOf(IReadOnlyList<IUIComponent> list)
   {
      for (var i = list.Count - 1; i >= 0; i--)
         yield return list[i];
   }
}
