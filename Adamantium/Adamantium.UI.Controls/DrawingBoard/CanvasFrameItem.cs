using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A titled box drawn BEHIND a group of nodes - the comment frame every graph editor has.
/// <para>What it is FOR: a graph of forty nodes is unreadable as forty nodes and readable as five labelled areas. It is
/// the only documentation a graph ever gets, and it is the cheapest thing in the editor to provide.</para>
/// <para>It holds nothing. A frame is not a container and does not own what is inside it - which is what makes it
/// harmless: nodes are added, moved and deleted without ever consulting it, and a frame drawn round a node that has
/// since moved away is simply a frame with nothing in it. What it DOES do is carry them: dragged, it takes along
/// whatever was standing on it when the drag began - see <see cref="Catch"/>.</para>
/// <para>In the band UNDER the controls, which is what <see cref="CanvasBand"/> exists for: a frame over its own nodes
/// would be a sheet of colour over the thing it is about.</para></summary>
public class CanvasFrameItem : ICanvasItem
{
    private readonly List<ICanvasItem> _caught = new();

    private Pen _pen;
    private Brush _penBrush;
    private double _penWidth = double.NaN;

    public CanvasFrameItem(Rect world, string title, Brush stroke, Brush fill = null)
    {
        World = world;
        Title = title ?? string.Empty;
        Stroke = stroke;
        Fill = fill;
    }

    /// <summary>The box it covers, in WORLD units.</summary>
    public Rect World { get; set; }

    /// <summary>What it says along the top. The whole point of the thing.</summary>
    public string Title { get; set; }

    public Brush Stroke { get; set; }

    /// <summary>What it is washed with, or null for an outline only. Meant to be nearly transparent: it is BEHIND the
    /// nodes and must not change how they read.</summary>
    public Brush Fill { get; set; }

    /// <summary>How thick the outline is, in WORLD units.</summary>
    public double Thickness { get; set; } = 2;

    /// <summary>How tall the strip with the title is, in WORLD units - and the part of the frame that is a HANDLE: the
    /// rest of it is see-through to the pointer, or a frame would swallow every press meant for what is inside it.
    /// </summary>
    public double TitleHeight { get; set; } = 26;

    /// <summary>Which graph this belongs to. A frame is a way of reading a graph and means nothing on a drawing.
    /// </summary>
    public CanvasMode Mode => CanvasMode.Nodes;

    /// <summary>UNDER the nodes. Over them it would be a sheet of colour across the thing it is about.</summary>
    public CanvasBand Band => CanvasBand.Under;

    /// <summary>Its own box. Resized by its grips like any other box.</summary>
    public Rect Bounds => World;

    string ICanvasItem.Title => string.IsNullOrEmpty(Title) ? "Frame" : $"Frame \"{Title}\"";

    /// <summary>ONLY THE TITLE STRIP answers the pointer. A frame is mostly empty by design - what is inside it is the
    /// point - and one that could be picked up by its middle would be a sheet nobody could click through.</summary>
    public bool HitTest(Vector2 world, double tolerance)
    {
        var strip = new Rect(World.X - tolerance, World.Y - tolerance,
            World.Width + tolerance * 2, TitleHeight + tolerance * 2);

        if (strip.Contains(world)) return true;

        // ...and its outline, so a frame can be grabbed by an edge the way anything else can.
        var inner = new Rect(World.X + tolerance, World.Y + tolerance,
            Math.Max(0, World.Width - tolerance * 2), Math.Max(0, World.Height - tolerance * 2));

        return World.Contains(world) && !inner.Contains(world);
    }

    public ICanvasItem Copy() => new CanvasFrameItem(World, Title, Stroke?.Copy(), Fill?.Copy())
    {
        Thickness = Thickness,
        TitleHeight = TitleHeight
    };

    /// <summary>Remembers what is standing on the frame RIGHT NOW, so a drag can take it along.
    /// <para>Taken once, when the drag begins, and not asked again while it runs: a frame that worked out what it holds
    /// on every move would pick up whatever it was pushed over on the way, and a node dragged out of a frame would be
    /// dragged back in by the frame catching up with it.</para></summary>
    public void Catch(IEnumerable<ICanvasItem> among)
    {
        _caught.Clear();
        if (among == null) return;

        foreach (var item in among)
        {
            if (ReferenceEquals(item, this) || item is ConnectionItem) continue;
            var box = item.Bounds;
            if (World.Contains(new Vector2(box.X, box.Y)) &&
                World.Contains(new Vector2(box.X + box.Width, box.Y + box.Height)))
            {
                _caught.Add(item);
            }
        }
    }

    /// <summary>Lets go of what was caught - the drag is over.</summary>
    public void Release() => _caught.Clear();

    public void Move(Vector2 worldDelta)
    {
        World = new Rect(World.X + worldDelta.X, World.Y + worldDelta.Y, World.Width, World.Height);

        foreach (var item in _caught) item.Move(worldDelta);
    }

    /// <summary>Resized like any box - and what is inside is LEFT WHERE IT IS. A frame is a note about a graph, not a
    /// layout: stretching the note must not move the work.</summary>
    public void Resize(Rect world)
    {
        if (world.Width <= 0 || world.Height <= 0) return;

        World = world;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (canvas == null || World.Width <= 0 || World.Height <= 0) return;

        var topLeft = canvas.WorldToScreen(new Vector2(World.X, World.Y));
        var bottomRight = canvas.WorldToScreen(new Vector2(World.X + World.Width, World.Y + World.Height));
        var box = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

        var width = Thickness > 0 ? Math.Max(Thickness * canvas.Scale, 1.0) : 0;
        session.DrawRectangle(Fill, box, width > 0 ? PenFor(width) : null);

        if (string.IsNullOrEmpty(Title)) return;

        // The title is drawn by a TEXT ITEM the frame keeps. Shaping a string is a layout with a cache behind it, and
        // there is already one thing here that knows how to do it - a second would be a second thing to keep right
        // about fonts, zoom and when a shaped string may be reused.
        _label ??= new TextItem(default, Title, Stroke, TitleHeight * 0.6);
        _label.Text = Title;
        _label.Brush = Stroke;
        _label.FontSize = TitleHeight * 0.6;

        // Inset from the corner, so it never sits on the outline.
        var inset = Thickness + 2;
        _label.Origin = new Vector2(World.X + inset, World.Y + inset);
        _label.Render(session, canvas);
    }

    private TextItem _label;

    private Pen PenFor(double width)
    {
        if (Stroke == null) return null;
        if (_pen != null && ReferenceEquals(_penBrush, Stroke) && Math.Abs(_penWidth - width) < 1e-6) return _pen;

        _penBrush = Stroke;
        _penWidth = width;
        _pen = new Pen(Stroke, width);

        return _pen;
    }
}
