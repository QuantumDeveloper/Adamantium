using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>WHAT CAN BE DONE TO WHAT IS SELECTED, in a bar that follows the selection frame rather than an edge of the
/// viewport.
/// <para>ACTIONS and not properties, with no exception: what a thing looks like belongs to the inspector, and saying it
/// in two places is two places to keep in step. A colour swatch stood here once and was exactly that - it could only
/// ever show ONE of the colours an object has (a shape's outline, as it happened), while a person looking at a green
/// rectangle read it as "the colour of this", and the panel says all three of them a few pixels away.</para>
/// <para>Everything here except the bin belongs to a DRAWING: in a graph a node's colour is its accent, paint order
/// between nodes is not something anybody reasons about since they are all in one band, and what groups a graph is a
/// comment frame rather than a group. So in a graph the bar is the one thing that still means something - take it
/// away.</para></summary>
public class CanvasSelectionBar : Control, ICanvasPart
{
    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasSelectionBar), new PropertyMetadata(null, OnCanvasChanged));

    /// <summary>Whether the canvas is being used as a DRAWING - which is what the actions that only mean something to
    /// one are shown by.</summary>
    public static readonly AdamantiumProperty IsDrawingProperty = AdamantiumProperty.Register(nameof(IsDrawing),
        typeof(Boolean), typeof(CanvasSelectionBar), new PropertyMetadata(true));

    /// <summary>The plane whose selection this bar acts on.</summary>
    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    public Boolean IsDrawing
    {
        get => GetValue<Boolean>(IsDrawingProperty);
        private set => SetCurrentValue(IsDrawingProperty, value);
    }

    private static void OnCanvasChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasSelectionBar bar) return;

        if (e.OldValue is InfiniteCanvas was) was.ModeChanged -= bar.OnModeChanged;
        if (e.NewValue is InfiniteCanvas now) now.ModeChanged += bar.OnModeChanged;

        bar.ReadMode();
    }

    private void OnModeChanged(object sender, EventArgs e) => ReadMode();

    private void ReadMode() => IsDrawing = Canvas is not { } canvas || canvas.Mode == CanvasMode.Drawing;
}
