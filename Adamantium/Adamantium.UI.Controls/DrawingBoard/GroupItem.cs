using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Several things on the plane treated as one.
/// <para>An item like any other, and that is the whole design: everything around it - the scene, the selection frame,
/// the paint order, the inspector - already knows how to deal with an item, so a group needs nothing new from any of
/// them. It is also what makes groups nest without a word: a group holding a group is a group holding an item.</para>
/// <para>What it does NOT do is own its children's place in paint order. The children leave the scene when the group is
/// made and come back when it is broken, which is what keeps "what is drawn" a single flat list that the renderer can
/// walk without asking anything about groups.</para></summary>
public class GroupItem : ICanvasItem
{
    private readonly List<ICanvasItem> _children = new();

    public GroupItem(IEnumerable<ICanvasItem> children)
    {
        if (children != null) _children.AddRange(children);
    }

    /// <summary>What is in it, in paint order - first drawn is underneath, exactly as in the scene they came from.
    /// </summary>
    public IReadOnlyList<ICanvasItem> Children => _children;

    /// <summary>The same things TOPMOST FIRST - what a panel showing the plane's structure puts under this one, read
    /// the same way round as the level above it. Only groups have it: a tree asks each node for it by name, and having
    /// nothing of the sort is what makes everything else a leaf.</summary>
    public IReadOnlyList<ICanvasItem> Inside
    {
        get
        {
            if (_inside != null) return _inside;

            var top = new List<ICanvasItem>(_children);

            top.Reverse();
            return _inside = top;
        }
    }

    private List<ICanvasItem> _inside;

    /// <summary>The colour of what is IN it, when everything in it agrees - and nothing when they do not, because a
    /// group of a red stroke and a blue one is not any one colour.</summary>
    public Color? Paint
    {
        get
        {
            Color? one = null;

            foreach (var child in _children)
            {
                if (child.Paint is not { } paint) continue;
                if (one is { } had && had != paint) return null;

                one = paint;
            }

            return one;
        }
    }

    /// <summary>Paints everything in it. A group is a handle on several things at once, and this is one of the
    /// things.</summary>
    public void PaintWith(Color color)
    {
        foreach (var child in _children) child.PaintWith(color);
    }

    /// <summary>A second group holding copies of what is in this one. Null when NOTHING in it could be copied: a group
    /// of things that cannot be copied is not a group, it is an empty box. What can be copied comes; what cannot is
    /// left behind, which is the same rule the copier follows one level up.</summary>
    public ICanvasItem Copy()
    {
        var copies = new List<ICanvasItem>();
        foreach (var child in _children)
        {
            if (child.Copy() is { } made) copies.Add(made);
        }

        return copies.Count == 0 ? null : new GroupItem(copies);
    }


    /// <summary>Where it stands in paint order - stamped by the scene. See ICanvasItem.Order.</summary>
    public int Order { get; set; }

    /// <summary>Everything its children cover. Asked rather than kept: a child that is moved through the inspector
    /// moves inside the group too, and a remembered box would be wrong from that moment on.</summary>
    public Rect Bounds
    {
        get
        {
            if (_children.Count == 0) return default;

            var box = _children[0].Bounds;
            for (var i = 1; i < _children.Count; i++) box = Union(box, _children[i].Bounds);

            return box;
        }
    }

    public string Title => $"Group ({_children.Count})";

    /// <summary>How many things are in it, for an inspector line that says what this is.</summary>
    public int Count => _children.Count;

    /// <summary>Where the group is and how big, one number at a time - the same lines every other item offers, and
    /// they mean here what they mean everywhere: moving takes the whole group, resizing takes it with everything in
    /// its place.</summary>
    public double X
    {
        get => Bounds.X;
        set => Move(new Vector2(value - Bounds.X, 0));
    }

    public double Y
    {
        get => Bounds.Y;
        set => Move(new Vector2(0, value - Bounds.Y));
    }

    public double Width
    {
        get => Bounds.Width;
        set => Resize(new Rect(Bounds.X, Bounds.Y, Math.Max(1e-9, value), Bounds.Height));
    }

    public double Height
    {
        get => Bounds.Height;
        set => Resize(new Rect(Bounds.X, Bounds.Y, Bounds.Width, Math.Max(1e-9, value)));
    }

    public void Move(Vector2 worldDelta)
    {
        foreach (var child in _children) child.Move(worldDelta);
    }

    /// <summary>Puts the whole group in a new box, and every child keeps its PLACE IN IT: a child a third of the way
    /// across stays a third of the way across, whatever the group is stretched to. Anything else and resizing a group
    /// would scatter it.</summary>
    public void Resize(Rect world)
    {
        var from = Bounds;
        if (world.Width <= 0 || world.Height <= 0 || from.Width <= 0 || from.Height <= 0) return;

        var sx = world.Width / from.Width;
        var sy = world.Height / from.Height;

        foreach (var child in _children)
        {
            var box = child.Bounds;

            child.Resize(new Rect(
                world.X + (box.X - from.X) * sx,
                world.Y + (box.Y - from.Y) * sy,
                Math.Max(1e-9, box.Width * sx),
                Math.Max(1e-9, box.Height * sy)));
        }
    }

    /// <summary>Hit when any CHILD is - not when the group's box is. A group of two things far apart has a box that is
    /// mostly empty, and picking it up by that emptiness would make everything under it unreachable.</summary>
    public bool HitTest(Vector2 world, double tolerance)
    {
        foreach (var child in _children)
        {
            if (child.HitTest(world, tolerance)) return true;
        }

        return false;
    }

    /// <summary>The child under a point, or null. What "entering" a group means: a second click goes past the group to
    /// the thing inside it. Topmost first, because that is what the eye picked.</summary>
    public ICanvasItem Pick(Vector2 world, double tolerance)
    {
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (_children[i].HitTest(world, tolerance)) return _children[i];
        }

        return null;
    }

    /// <summary>Takes one thing out, wherever in the nest it is. The children are NOT in the scene, so the scene cannot
    /// remove them - and a delete that quietly did nothing because the thing was inside a group would be the worst
    /// possible answer.</summary>
    public bool Remove(ICanvasItem item)
    {
        if (item == null) return false;

        if (_children.Remove(item))
        {
            _inside = null;
            return true;
        }

        foreach (var child in _children)
        {
            if (child is GroupItem nested && nested.Remove(item)) return true;
        }

        return false;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        foreach (var child in _children) child.Render(session, canvas);
    }

    private static Rect Union(Rect a, Rect b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);

        return new Rect(left, top, right - left, bottom - top);
    }
}
