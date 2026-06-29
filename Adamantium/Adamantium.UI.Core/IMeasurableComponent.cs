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