using System.Collections.Generic;

namespace Adamantium.UI.Controls;

/// <summary>One line of an inspector, written. What it holds is the OLD value and the NEW one for every object the line
/// was pointed at, and it puts them back through the very binding the line writes through.
/// <para>Its own kind of step because a comparison of the drawing cannot see it: a colour, a thickness, the words on a
/// label - none of them move anything, so the canvas's before-and-after of where things are would record a change of
/// nothing at all. What DOES know is the grid that wrote it, at the moment it wrote it.</para></summary>
public sealed class CanvasPropertyStep : ICanvasStep
{
    private readonly PropertyGrid _grid;
    private readonly PropertyDefinition _definition;
    private readonly List<(object Target, object Was, object Is)> _values;

    public CanvasPropertyStep(PropertyGrid grid, PropertyDefinition definition,
        List<(object Target, object Was, object Is)> values)
    {
        _grid = grid;
        _definition = definition;
        _values = values;
        Reason = definition?.Header as string ?? "Property";
    }

    public string Reason { get; }

    /// <summary>Whether any of the objects actually took a different value. A line re-typed with the same number in it
    /// must not become a step that undoes to what it already was.</summary>
    public bool IsSomething
    {
        get
        {
            foreach (var (_, was, now) in _values)
            {
                if (!Equals(was, now)) return true;
            }

            return false;
        }
    }

    public void Apply(ICanvasScene scene, bool forward)
    {
        foreach (var (target, was, now) in _values) _grid.WriteTo(target, _definition, forward ? now : was);

        // The objects on the plane are DATA and say nothing when they change - that is the whole arrangement - so both
        // the drawing and the PANEL are told here, once, rather than by each of them. Without the second, the shape
        // goes back and the line above it still shows the number that was undone.
        //
        // REBUILT and not merely re-read: a row holds a live binding to a plain property that raises nothing, so what
        // it has is what the binding pushed when it was made. Re-reading asks the row, and the row answers with the
        // stale number it is holding; only pointing the binding at the object again goes back to the object. Once per
        // undo, which is not a cost worth avoiding.
        scene?.Touch();
        _grid.Rebuild();
    }
}
