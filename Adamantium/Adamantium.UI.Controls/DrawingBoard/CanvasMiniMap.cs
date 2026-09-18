using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>The WHOLE of what is on the plane, drawn small, with a box round the part of it you are looking at.
/// <para>A plane with no edges has no scrollbars, and a scrollbar is what normally tells you two things at once: how
/// much there is, and where in it you are. On a graph of any size those are the two questions asked most often, and
/// this is the only thing that answers them. Pressing it takes the camera there.</para>
/// <para>Every item as a plain BOX and nothing else. A small picture of the drawing would be a second renderer to keep
/// in step with the first; boxes say where things are, which is all a map is for, and they cost one rectangle each.
/// </para></summary>
public class CanvasMiniMap : InputUIComponent, ICanvasPart
{
    private Rect _world;
    private double _scale;
    private Vector2 _origin;

    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasMiniMap),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnCanvasChanged));

    public static readonly AdamantiumProperty ItemBrushProperty = AdamantiumProperty.Register(nameof(ItemBrush),
        typeof(Brush), typeof(CanvasMiniMap), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty ViewBrushProperty = AdamantiumProperty.Register(nameof(ViewBrush),
        typeof(Brush), typeof(CanvasMiniMap), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>The plane this is a map of.</summary>
    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    /// <summary>What the things on the plane are drawn in.</summary>
    public Brush ItemBrush
    {
        get => GetValue<Brush>(ItemBrushProperty);
        set => SetValue(ItemBrushProperty, value);
    }

    /// <summary>What the box round the part being looked at is drawn in.</summary>
    public Brush ViewBrush
    {
        get => GetValue<Brush>(ViewBrushProperty);
        set => SetValue(ViewBrushProperty, value);
    }

    public CanvasMiniMap()
    {
        MouseDown += OnPressed;
        MouseMove += OnMoved;
        MouseUp += OnReleased;
    }

    private static void OnCanvasChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasMiniMap map) return;

        if (e.OldValue is InfiniteCanvas was)
        {
            was.CameraChanged -= map.OnCameraChanged;
            was.PlaneChanged -= map.OnCameraChanged;
        }

        if (e.NewValue is InfiniteCanvas now)
        {
            now.CameraChanged += map.OnCameraChanged;

            // ...and what is ON the plane, not only where the camera is. A node put down while the camera stood still
            // changed nothing the map was listening to, so the map went on showing the graph as it had been.
            now.PlaneChanged += map.OnCameraChanged;
        }
    }

    // The map is a picture of where the camera is, so it is redrawn when the camera moves - and when the plane changes,
    // which the canvas repaints for anyway and this is a child of the same tree.
    private void OnCameraChanged(object sender, EventArgs e) => InvalidateRender(false);

    protected override void OnRender(IDrawingContext context)
    {
        base.OnRender(context);

        Draw(context.ForControl(this));
    }

    /// <summary>Draws the map into a session - the same shape as everything on the plane, so what a map puts on the
    /// screen can be asked about without a device.</summary>
    internal void Draw(IDrawingSession session)
    {
        var size = RenderSize;
        if (session == null || Canvas is not { } canvas || size.Width <= 1 || size.Height <= 1) return;

        if (!Spread(canvas, out _world)) return;

        // One scale for both axes, so the map is not a squashed picture of the plane: which way round things are is the
        // whole of what a map says.
        _scale = Math.Min(size.Width / _world.Width, size.Height / _world.Height);
        _origin = new Vector2(
            (size.Width - _world.Width * _scale) / 2,
            (size.Height - _world.Height * _scale) / 2);

        foreach (var item in canvas.ItemsHere())
        {
            var box = item.Bounds;
            if (box.Width <= 0 && box.Height <= 0) continue;

            session.DrawRectangle(ItemBrush, Small(box));
        }

        // The VIEWPORT last, over the top: it is the one thing on the map that is about the reader rather than the
        // drawing.
        if (ViewBrush != null) session.DrawRectangle(null, Small(canvas.VisibleWorld), new Pen(ViewBrush, 1));
    }

    private Rect Small(Rect world) =>
        new(_origin.X + (world.X - _world.X) * _scale,
            _origin.Y + (world.Y - _world.Y) * _scale,
            Math.Max(1, world.Width * _scale),
            Math.Max(1, world.Height * _scale));

    // Everything on the plane AND what can be seen of it, together: a map that showed only the drawing would have no
    // room to show a camera that has wandered off it, and "I am lost" is exactly when a map is looked at.
    private static bool Spread(InfiniteCanvas canvas, out Rect world)
    {
        var seen = canvas.VisibleWorld;
        double left = seen.X, top = seen.Y, right = seen.X + seen.Width, bottom = seen.Y + seen.Height;

        foreach (var item in canvas.ItemsHere())
        {
            var box = item.Bounds;

            left = Math.Min(left, box.X);
            top = Math.Min(top, box.Y);
            right = Math.Max(right, box.X + box.Width);
            bottom = Math.Max(bottom, box.Y + box.Height);
        }

        // A margin, so nothing is drawn against the very edge of the map and the viewport box is visible when it is at
        // the far corner.
        var room = Math.Max((right - left), (bottom - top)) * 0.05;
        world = new Rect(left - room, top - room, right - left + room * 2, bottom - top + room * 2);

        return world.Width > 1e-6 && world.Height > 1e-6;
    }

    private bool _dragging;

    // PRESSING THE MAP takes the camera there, and dragging keeps taking it - which is the whole interaction. A map you
    // can only look at answers half the question it raises.
    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButtons.Left || Canvas == null) return;

        _dragging = true;
        CaptureMouse();
        Look(e.GetPosition(this));

        e.Handled = true;
    }

    private void OnMoved(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;

        Look(e.GetPosition(this));
        e.Handled = true;
    }

    private void OnReleased(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;

        _dragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>Points the camera at whatever is under a place ON THE MAP. What a press does, and the whole of what a
    /// press does - so this is where the arithmetic is, and a test can ask it without a window.</summary>
    internal void Look(Vector2 at)
    {
        if (Canvas == null || _scale <= 0) return;

        Canvas.CenterOn(new Vector2(
            _world.X + (at.X - _origin.X) / _scale,
            _world.Y + (at.Y - _origin.Y) / _scale));
    }
}
