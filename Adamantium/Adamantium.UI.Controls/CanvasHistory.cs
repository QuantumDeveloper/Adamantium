using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>One undoable thing. Its own contract so that the history is not limited to what the canvas can work out by
/// COMPARING the drawing: a change that is not about where things are - a colour, a thickness, the words in a label -
/// leaves no trace in a comparison, and whoever made it is the one who knows how to take it back.</summary>
public interface ICanvasStep
{
    /// <summary>What it was, for a menu that says so.</summary>
    string Reason { get; }

    /// <summary>Put it back (<paramref name="forward"/> false) or do it again.</summary>
    void Apply(ICanvasScene scene, bool forward);
}

/// <summary>What was done to the drawing, so it can be undone.
/// <para>Held by the APPLICATION, like the scene and for the same reason: undo belongs to whoever owns the drawing. The
/// canvas only opens and closes the steps - see <see cref="InfiniteCanvas.BeginEdit"/> - because only it knows where a
/// GESTURE begins and ends, and that is what makes one drag of ten things one step rather than ten thousand.</para>
/// <para>A step is a BEFORE and an AFTER, taken by comparison: what the scene held and in what order, and where each
/// thing was. Nothing new is asked of an item, so a third-party <see cref="ICanvasItem"/> is undoable without knowing
/// this exists.</para></summary>
public sealed class CanvasHistory
{
    private readonly List<ICanvasStep> _done = new();
    private readonly List<ICanvasStep> _undone = new();

    private int _limit = 200;

    /// <summary>How many steps are kept. Drawing is a long session and a step holds a list the size of the scene, so
    /// this is a memory ceiling rather than a rule about what is worth remembering.</summary>
    public int Limit
    {
        get => _limit;
        set
        {
            _limit = Math.Max(1, value);
            Trim();
        }
    }

    /// <summary>Raised whenever there is something new to undo or redo - what a button watches to enable itself.</summary>
    public event EventHandler Changed;

    public bool CanUndo => _done.Count > 0;

    public bool CanRedo => _undone.Count > 0;

    /// <summary>What the next undo would put back, for a menu that says so. Empty when there is nothing.</summary>
    public string NextUndo => _done.Count > 0 ? _done[^1].Reason : string.Empty;

    public string NextRedo => _undone.Count > 0 ? _undone[^1].Reason : string.Empty;

    /// <summary>Forgets everything - a new drawing, or one just loaded.</summary>
    public void Clear()
    {
        if (_done.Count == 0 && _undone.Count == 0) return;

        _done.Clear();
        _undone.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Undo(ICanvasScene scene)
    {
        if (scene == null || _done.Count == 0) return false;

        var step = _done[^1];
        _done.RemoveAt(_done.Count - 1);
        step.Apply(scene, forward: false);
        _undone.Add(step);

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo(ICanvasScene scene)
    {
        if (scene == null || _undone.Count == 0) return false;

        var step = _undone[^1];
        _undone.RemoveAt(_undone.Count - 1);
        step.Apply(scene, forward: true);
        _done.Add(step);

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Records ONE change to the scene as a step. For a change that happens all at once - a clear, a paste, a
    /// line written in a panel; a GESTURE is spread over many events and the canvas opens and closes its own step
    /// around it instead (<see cref="InfiniteCanvas.BeginEdit"/>).</summary>
    public void Record(ICanvasScene scene, string reason, Action change)
    {
        if (scene == null || change == null) return;

        CanvasStep.Snapshot(scene, out var before, out var wasAt);
        change();
        CanvasStep.Snapshot(scene, out var after, out var isAt);

        Push(new CanvasStep(reason, before, after, wasAt, isAt));
    }

    /// <summary>Adds a step. A new one throws away whatever was undone: the drawing has taken a different turn, and a
    /// redo onto it would put back something that no longer follows from anything.
    /// <para>Public, because the canvas is not the only thing that changes a drawing: an inspector writing a colour
    /// records its own step, and so may an application with edits of its own.</para></summary>
    public void Push(ICanvasStep step)
    {
        if (step == null) return;
        if (step is CanvasStep { IsSomething: false } or CanvasPropertyStep { IsSomething: false }) return;

        _done.Add(step);
        _undone.Clear();
        Trim();

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Trim()
    {
        while (_done.Count > _limit) _done.RemoveAt(0);
    }
}

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
