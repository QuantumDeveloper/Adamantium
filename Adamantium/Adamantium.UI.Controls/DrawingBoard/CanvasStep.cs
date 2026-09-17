using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>One undoable step: what the drawing held before and what it holds now.</summary>
internal sealed class CanvasStep : ICanvasStep
{
    private readonly List<ICanvasItem> _before;
    private readonly List<ICanvasItem> _after;
    private readonly Dictionary<ICanvasItem, Rect> _wasAt;
    private readonly Dictionary<ICanvasItem, Rect> _isAt;

    internal CanvasStep(string reason, List<ICanvasItem> before, List<ICanvasItem> after,
        Dictionary<ICanvasItem, Rect> wasAt, Dictionary<ICanvasItem, Rect> isAt)
    {
        Reason = reason ?? string.Empty;
        _before = before;
        _after = after;
        _wasAt = wasAt;
        _isAt = isAt;
    }

    public string Reason { get; }

    /// <summary>What the scene holds and where each thing is, right now. The one place either half of a step is taken,
    /// so the canvas's gesture steps and an application's one-shot steps cannot disagree about what a step is.</summary>
    internal static void Snapshot(ICanvasScene scene, out List<ICanvasItem> items, out Dictionary<ICanvasItem, Rect> at)
    {
        items = new List<ICanvasItem>(scene.ItemsIn(Everything));
        at = new Dictionary<ICanvasItem, Rect>(items.Count);

        foreach (var item in items) at[item] = item.Bounds;
    }

    // Everything there is: a step is about the whole drawing, not about the part of it that can be seen.
    private static Rect Everything =>
        new(double.MinValue / 4, double.MinValue / 4, double.MaxValue / 2, double.MaxValue / 2);

    /// <summary>Whether anything actually happened. A gesture that changed nothing - a click that selected and let go -
    /// must not fill the history with steps that undo to the same drawing.</summary>
    internal bool IsSomething => _before.Count != _after.Count || Moved() || Reordered();

    public void Apply(ICanvasScene scene, bool forward)
    {
        scene.Reset(forward ? _after : _before);

        // Position LAST: what is put back has to be in the scene before it is asked to move, and an item that left the
        // drawing has nowhere to be moved to.
        foreach (var (item, box) in forward ? _isAt : _wasAt) PutBack(item, box);

        scene.Touch();
    }

    private static void PutBack(ICanvasItem item, Rect box)
    {
        var now = item.Bounds;
        if (Same(now, box)) return;

        // A MOVE is exact and a resize is not always - a stroke resized back is scaled back, not restored point by
        // point - so anything that only travelled is put back by travelling.
        if (Math.Abs(now.Width - box.Width) < 1e-9 && Math.Abs(now.Height - box.Height) < 1e-9)
        {
            item.Move(new Vector2(box.X - now.X, box.Y - now.Y));
            return;
        }

        item.Resize(box);
    }

    private bool Moved()
    {
        foreach (var (item, box) in _wasAt)
        {
            if (_isAt.TryGetValue(item, out var now) && !Same(box, now)) return true;
        }

        return false;
    }

    private bool Reordered()
    {
        for (var i = 0; i < _before.Count; i++)
        {
            if (!ReferenceEquals(_before[i], _after[i])) return true;
        }

        return false;
    }

    private static bool Same(Rect a, Rect b) =>
        Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9 &&
        Math.Abs(a.Width - b.Width) < 1e-9 && Math.Abs(a.Height - b.Height) < 1e-9;
}
