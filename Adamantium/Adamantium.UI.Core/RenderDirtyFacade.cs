using System.Collections.Generic;

namespace Adamantium.UI.Core;

/// <summary>
/// The name everything invalidating itself already calls - now a ROUTER over per-stage <see cref="RenderDirtyScope"/>s
/// rather than one shared set (see <see cref="RenderDirtyRouter"/>). A mark goes to the scope of the component that
/// makes it; a question is asked of a scope, so a cache asks its OWN.
/// <para>Kept as the single spelling on purpose: a control states a fact about itself ("my geometry changed") and has no
/// business knowing which stage it is drawn by, or that stages exist at all.</para>
/// </summary>
public static class RenderDirty
{
    /// <summary>The window content's marks - the scope everything belongs to until a stage claims it.</summary>
    public static RenderDirtyScope Scope => RenderDirtyRouter.Default;

    public static void MarkGeometry(IUIComponent component) => RenderDirtyRouter.Of(component).MarkGeometry(component);

    public static void MarkPaint(IUIComponent component) => RenderDirtyRouter.Of(component).MarkPaint(component);

    /// <summary>Something MOVED. Without a component the mover has no owner to name (a bare Transform ticking), and no
    /// scope can be ruled out - so they all hear it, exactly as with an unnameable structural change.</summary>
    public static void MarkTransform(IUIComponent component)
    {
        if (component != null)
        {
            RenderDirtyRouter.Of(component).MarkTransform(component);
            return;
        }

        foreach (var scope in RenderDirtyRouter.All()) scope.MarkTransform(null);
    }

    public static void MarkNodeTransform(IUIComponent node) => RenderDirtyRouter.Of(node).MarkNodeTransform(node);

    /// <summary>A structural change. WITHOUT a component it is an unnameable one - something changed about the drawn set
    /// and nobody can say what - so it reaches EVERY scope: no owner can be ruled out.</summary>
    public static void MarkStructural(IUIComponent component = null)
    {
        if (component != null)
        {
            RenderDirtyRouter.Of(component).MarkStructural(component);
            return;
        }

        foreach (var scope in RenderDirtyRouter.All()) scope.MarkStructural();
    }

    public static void MarkDetached() => RenderDirtyRouter.Default.MarkDetached();

    public static long DetachGeneration => RenderDirtyRouter.Default.DetachGeneration;

    public static void SnapshotGeometryInto(List<IUIComponent> buffer) => RenderDirtyRouter.Default.SnapshotGeometryInto(buffer);

    public static void SnapshotPaintInto(List<IUIComponent> buffer) => RenderDirtyRouter.Default.SnapshotPaintInto(buffer);

    public static void SnapshotMovedInto(List<IUIComponent> buffer) => RenderDirtyRouter.Default.SnapshotMovedInto(buffer);

    public static void SnapshotNodesInto(List<IUIComponent> buffer) => RenderDirtyRouter.Default.SnapshotNodesInto(buffer);

    public static void SnapshotStructuralInto(List<IUIComponent> buffer) => RenderDirtyRouter.Default.SnapshotStructuralInto(buffer);

    public static int GeometryCount => RenderDirtyRouter.Default.GeometryCount;

    public static int PaintCount => RenderDirtyRouter.Default.PaintCount;

    public static int StructuralCount => RenderDirtyRouter.Default.StructuralCount;

    public static long TotalStructuralMarks => RenderDirtyRouter.Default.TotalStructuralMarks;

    public static IReadOnlyCollection<IUIComponent> MovedNodes => RenderDirtyRouter.Default.MovedNodes;

    public static IReadOnlyCollection<IUIComponent> Geometry => RenderDirtyRouter.Default.Geometry;

    public static bool HasWork => RenderDirtyRouter.Default.HasWork;

    /// <summary>Does ANY stage have work - the content, the popups, the adorners? This is the loop's question, and it is
    /// not the same as the content's: a menu whose item just lit up marks its own scope, and a loop that only asked the
    /// content would go back to sleep with that frame still owed. (Seen as a popup whose close button appeared only on
    /// the second opening: the build that was owed simply never ran.)</summary>
    public static bool AnyHasWork
    {
        get
        {
            foreach (var scope in RenderDirtyRouter.All())
                if (scope.HasWork) return true;

            return false;
        }
    }

    public static bool IsStructural => RenderDirtyRouter.Default.IsStructural;

    public static bool IsTransform => RenderDirtyRouter.Default.IsTransform;

    public static bool IsTransformUnknown => RenderDirtyRouter.Default.IsTransformUnknown;

    public static bool IsStructuralUnknown => RenderDirtyRouter.Default.IsStructuralUnknown;

    public static bool IsSettlingStructural => RenderDirtyRouter.Default.IsSettlingStructural;

    /// <summary>A multi-frame state swap has begun (a theme, a DPI change). It re-styles EVERYTHING, so every scope has
    /// to keep walking in full until the cascade drains - a window whose scope was not told would keep replaying the
    /// frame it recorded under the old theme.</summary>
    public static void ForceStructuralUntilSettled()
    {
        foreach (var scope in RenderDirtyRouter.All()) scope.ForceStructuralUntilSettled();
    }

    /// <summary>...and the layout says the cascade has drained. Also everyone's business, for the same reason.</summary>
    public static void NotifyLayoutQuiescent()
    {
        foreach (var scope in RenderDirtyRouter.All()) scope.NotifyLayoutQuiescent();
    }

    /// <summary>Clears EVERY scope's marks - the once-per-frame clear, after every window and every stage has recorded.
    /// Per-scope clearing is what makes "clear as soon as my own record is done" possible; until the stages own their
    /// recording that stays one call.</summary>
    public static void Clear()
    {
        foreach (var scope in RenderDirtyRouter.All()) scope.Clear();
    }
}
