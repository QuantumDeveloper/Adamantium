using System;
using System.Globalization;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>THE VIEW BAR of an <see cref="InfiniteCanvas"/>: what was done and undone, how far in you are, and the way
/// back to the origin.
/// <para>Every button on it is one of the canvas's OWN commands - steering the camera is the canvas's business, since
/// zooming about the middle of what can be seen needs to know where that middle is once a docked panel has taken part
/// of the edge - and each command says when it can be pressed, so undo with nothing behind it switches its button off
/// without anyone asking.</para>
/// <para>Undo and redo live HERE rather than in the selection bar: they are about the drawing as a whole, and the
/// selection bar is not there when nothing is selected - which is exactly when the last thing done most often needs
/// taking back.</para></summary>
public class CanvasViewBar : Control, ICanvasPart
{
    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasViewBar), new PropertyMetadata(null, OnCanvasChanged));

    /// <summary>The zoom in words, as the bar shows it. A MULTIPLIER from one and not a percentage: everything else
    /// about the camera is stated that way, and at the far end a percentage stops reading at all - 0.01 shown as "1%"
    /// looks like a hundredth of a percent rather than a hundredth of full size.</summary>
    public static readonly AdamantiumProperty ZoomTextProperty = AdamantiumProperty.Register(nameof(ZoomText),
        typeof(String), typeof(CanvasViewBar), new PropertyMetadata("1x"));

    /// <summary>The plane this bar steers.</summary>
    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    public String ZoomText
    {
        get => GetValue<String>(ZoomTextProperty);
        private set => SetCurrentValue(ZoomTextProperty, value);
    }

    private static void OnCanvasChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasViewBar bar) return;

        if (e.OldValue is InfiniteCanvas was) was.CameraChanged -= bar.OnCameraChanged;
        if (e.NewValue is InfiniteCanvas now) now.CameraChanged += bar.OnCameraChanged;

        bar.Read();
    }

    private void OnCameraChanged(object sender, EventArgs e) => Read();

    private void Read()
    {
        ZoomText = Canvas is not { } canvas
            ? "1x"
            : String.Format(CultureInfo.InvariantCulture, "{0:0.###}x", canvas.Scale);
    }
}
