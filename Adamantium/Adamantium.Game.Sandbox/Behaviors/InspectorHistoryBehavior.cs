using System.Collections.Generic;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>Puts what an inspector writes into the drawing's history.
/// <code>&lt;PropertyGrid.Behaviors&gt;&lt;local:InspectorHistoryBehavior History="{Binding History}" Scene="{Binding Scene}"/&gt;&lt;/PropertyGrid.Behaviors&gt;</code>
/// </summary>
/// <remarks>
/// The canvas records a step by COMPARING the drawing before and after a gesture, and that catches everything a gesture
/// does - things arriving, leaving, moving, changing size. It catches nothing an inspector does: a colour, a thickness,
/// the words on a label change no bounds at all, so the comparison sees a drawing that did not change.
///
/// The one moment at which the old value still exists is just before the write, which is what ValueChanging is for. The
/// step then holds the old and the new for every object the line was pointed at, and puts them back through the same
/// binding the line writes through.
/// </remarks>
public class InspectorHistoryBehavior : Behavior<PropertyGrid>
{
    public static readonly AdamantiumProperty HistoryProperty = AdamantiumProperty.Register(nameof(History),
        typeof(CanvasHistory), typeof(InspectorHistoryBehavior), new PropertyMetadata(null));

    public static readonly AdamantiumProperty SceneProperty = AdamantiumProperty.Register(nameof(Scene),
        typeof(ICanvasScene), typeof(InspectorHistoryBehavior), new PropertyMetadata(null));

    private readonly List<(object Target, object Was, object Is)> _values = new();

    private PropertyGrid _grid;

    /// <summary>Where the step goes.</summary>
    public CanvasHistory History
    {
        get => GetValue<CanvasHistory>(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    /// <summary>The drawing being edited - told to repaint when a step is put back.</summary>
    public ICanvasScene Scene
    {
        get => GetValue<ICanvasScene>(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    protected override void OnAttached(PropertyGrid grid)
    {
        _grid = grid;
        grid.ValueChanging += OnValueChanging;
        grid.ValueChanged += OnValueChanged;
    }

    protected override void OnDetached(PropertyGrid grid)
    {
        grid.ValueChanging -= OnValueChanging;
        grid.ValueChanged -= OnValueChanged;
        _grid = null;
    }

    private void OnValueChanging(object sender, PropertyValuesChangedEventArgs e)
    {
        _values.Clear();
        if (History == null || _grid == null || e.Property == null) return;

        foreach (var target in e.Targets)
        {
            if (target is ICanvasItem) _values.Add((target, _grid.ValueOf(target, e.Property), null));
        }
    }

    private void OnValueChanged(object sender, PropertyValuesChangedEventArgs e)
    {
        if (History == null || _grid == null || _values.Count == 0) return;

        for (var i = 0; i < _values.Count; i++)
        {
            var (target, was, _) = _values[i];
            _values[i] = (target, was, _grid.ValueOf(target, e.Property));
        }

        // A COPY: the list is reused for the next write, and a step holding the live one would be rewritten by it.
        History.Push(new CanvasPropertyStep(_grid, e.Property,
            new List<(object, object, object)>(_values)));

        _values.Clear();
    }
}
