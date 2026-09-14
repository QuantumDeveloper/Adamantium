using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>A scene that keeps its items in a list and rejects by bounds. Everything an application needs until a
/// drawing outgrows a walk - and the point of <see cref="ICanvasScene"/> is that when it does, an index goes in HERE
/// and the canvas is not touched.
/// <para>Linear ON PURPOSE rather than as a placeholder: an index costs memory and a rebuild on every edit, and which
/// index to build is a question about how a real drawing is shaped. The walk is what says when that question is worth
/// answering - see the measurement the plan asks for.</para></summary>
public class CanvasScene : ICanvasScene
{
    private readonly List<ICanvasItem> _items = new();

    public IReadOnlyList<ICanvasItem> Items => _items;

    public event EventHandler Changed;

    public IEnumerable<ICanvasItem> ItemsIn(Rect world)
    {
        foreach (var item in _items)
        {
            if (Meets(item.Bounds, world)) yield return item;
        }
    }

    public void Add(ICanvasItem item)
    {
        if (item == null) return;

        _items.Add(item);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(ICanvasItem item)
    {
        if (!_items.Remove(item)) return false;

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Replace(ICanvasItem item, IReadOnlyList<ICanvasItem> pieces)
    {
        var at = _items.IndexOf(item);
        if (at < 0) return false;

        _items.RemoveAt(at);
        for (var i = 0; i < pieces.Count; i++) _items.Insert(at + i, pieces[i]);

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        if (_items.Count == 0) return;

        _items.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The topmost item under a world point, or nothing. Paint order is the list's order, so the LAST one that
    /// answers is the one on top.</summary>
    public ICanvasItem HitTest(Vector2 world, double tolerance)
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].HitTest(world, tolerance)) return _items[i];
        }

        return null;
    }

    /// <summary>Says that something already in the scene has changed - a stroke still being drawn, an item moved. The
    /// scene itself cannot notice: what an item holds is the item's business.</summary>
    public void Touch() => Changed?.Invoke(this, EventArgs.Empty);

    // Touching counts: a stroke exactly on the edge of the viewport is visible, and an item with no thickness in one
    // direction (a horizontal line) has a zero-height box that must still meet the world it lies in.
    private static bool Meets(Rect item, Rect world) =>
        item.X <= world.X + world.Width && item.X + item.Width >= world.X &&
        item.Y <= world.Y + world.Height && item.Y + item.Height >= world.Y;
}
