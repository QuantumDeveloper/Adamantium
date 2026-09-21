using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>What was done to the drawing, so it can be undone.
/// <para>Held by the APPLICATION, like the scene and for the same reason: undo belongs to whoever owns the drawing. The
/// canvas only opens and closes the steps - see <see cref="InfiniteCanvas.BeginEdit"/> - because only it knows where a
/// GESTURE begins and ends, and that is what makes one drag of ten things one step rather than ten thousand.</para>
/// <para>A step is a BEFORE and an AFTER, taken by comparison: what the scene held and in what order, and where each
/// thing was. Nothing new is asked of an item, so a third-party <see cref="ICanvasItem"/> is undoable without knowing
/// this exists.</para></summary>
public sealed class CanvasHistory : System.ComponentModel.INotifyPropertyChanged
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

    /// <summary>Raised whenever there is something new to undo or redo - what a button watches to enable itself.
    /// </summary>
    public event EventHandler Changed;

    /// <summary>...and the same news as a property change, so a button can simply BIND its enabled state to
    /// <see cref="CanUndo"/> instead of being wired to the event by hand.</summary>
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

    // Both, always: the two are the same news said twice, and a raise that told only one of them would leave whichever
    // listener used the other one stale.
    private void Announce()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CanUndo)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CanRedo)));
    }

    /// <summary>Whether there is anything left to take back. A button with nothing to undo says so by being off: a
    /// press that does nothing is a press that leaves a person wondering what they missed.</summary>
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
        Announce();
    }

    public bool Undo(ICanvasScene scene)
    {
        if (scene == null || _done.Count == 0) return false;

        var step = _done[^1];
        _done.RemoveAt(_done.Count - 1);
        step.Apply(scene, forward: false);
        _undone.Add(step);

        Announce();
        return true;
    }

    public bool Redo(ICanvasScene scene)
    {
        if (scene == null || _undone.Count == 0) return false;

        var step = _undone[^1];
        _undone.RemoveAt(_undone.Count - 1);
        step.Apply(scene, forward: true);
        _done.Add(step);

        Announce();
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

        Announce();
    }

    private void Trim()
    {
        while (_done.Count > _limit) _done.RemoveAt(0);
    }
}
