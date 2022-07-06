using System;
using Adamantium.UI.Input;

namespace Adamantium.UI;

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