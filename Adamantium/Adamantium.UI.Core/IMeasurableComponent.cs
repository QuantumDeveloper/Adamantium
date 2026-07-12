using Adamantium.Mathematics;

namespace Adamantium.UI.Core;

public interface IMeasurableComponent : IObservableComponent
{
    Double Width { get; set; }
    Double Height { get; set; }
    Double ActualWidth { get; }
    Double ActualHeight { get; }
    Double MinWidth { get; set; }
    Double MinHeight { get; set; }
    Double MaxWidth { get; set; }
    Double MaxHeight { get; set; }
    Thickness Margin { get; set; }
    VerticalAlignment VerticalAlignment { get; set; }
    HorizontalAlignment HorizontalAlignment { get; set; }
    object Tag { get; set; }

    bool UseLayoutRounding { get; set; }
    bool IsMeasureValid { get; }
    bool IsArrangeValid { get; }

    Size DesiredSize { get; }

    /// <summary>The rect this element was last arranged with (its last correct slot), or null if never arranged. The
    /// layout manager re-arranges an arrange-dirty element into this slot rather than guessing one - the element's own
    /// ArrangeOverride then re-distributes correct rects to its children.</summary>
    Rect? PreviousArrangeSlot { get; }

    /// <summary>The available size this element was last measured with (its cached constraint), or null if never
    /// measured. The layout manager re-measures a measure-dirty element with this rather than guessing one; only if the
    /// re-measure changes <see cref="DesiredSize"/> does it then invalidate the parent (whose measure depends on it).</summary>
    Size? PreviousMeasureConstraint { get; }
    
    /// <summary>
    /// A MEASURE BOUNDARY: this element's own <see cref="DesiredSize"/> cannot change as a result of its children
    /// re-measuring, so a child's size change must NOT propagate an <see cref="InvalidateMeasure"/> up into it. This is
    /// the standard layout-boundary concept (WPF derives the same thing): an element whose size is fixed EXTERNALLY -
    /// not read from content - isolates the measure of its subtree from its own. The default
    /// (<see cref="MeasurableUIComponent"/>) derives it automatically from a fixed Width AND Height; a virtualizing
    /// items host overrides it because its extent is count×cell - a function of the item count and its (uniform) cell,
    /// computed inside its own MeasureOverride, not of any one tile's measured size.
    ///
    /// Caveat for the items-host case: the cell is auto-sized from the first realized child, so the extent is strictly
    /// child-independent only once that (uniform) cell has settled - which it does on the first tile and never moves
    /// for a uniform template. For non-uniform items it is a one-frame approximation (the standard virtualization
    /// tradeoff), corrected by the panel's own next-frame re-measure.
    ///
    /// Honored on the queue-drain re-measure path (<c>LayoutManager.MeasureDirty</c>): a tile whose bind-time
    /// invalidation is drained a later iteration can't spuriously re-dirty the panel and spin the pass - the panel
    /// drains its realize backlog one slice PER FRAME (via InvalidateMeasureNextPass) as designed, instead of looping
    /// to MaxPassIterations in a single pass.
    /// </summary>
    bool IsMeasureBoundary { get; }

    void InvalidateMeasure();
    void InvalidateArrange();
    
    /// <summary>
    /// Carries out a measure of the control.
    /// </summary>
    /// <param name="availableSize">The available size for the control.</param>
    /// <param name="force">
    /// If true, the control will be measured even if <paramref name="availableSize"/> has not
    /// changed from the last measure.
    /// </param>
    void Measure(Size availableSize, bool force = false);

    /// <summary>
    /// Arranges the control and its children.
    /// </summary>
    /// <param name="rect">The control's new bounds.</param>
    /// <param name="force">
    /// If true, the control will be arranged even if <paramref name="rect"/> has not changed
    /// from the last arrange.
    /// </param>
    void Arrange(Rect rect, bool force = false);
}