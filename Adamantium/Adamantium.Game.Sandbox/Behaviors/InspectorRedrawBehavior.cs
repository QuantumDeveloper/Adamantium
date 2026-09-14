using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>
/// View-layer behavior: tells the canvas's SCENE that an inspector has just edited something in it, so the plane
/// repaints.
/// <code>&lt;PropertyGrid&gt;&lt;PropertyGrid.Behaviors&gt;&lt;local:InspectorRedrawBehavior Scene="{Binding Scene}"/&gt;&lt;/PropertyGrid.Behaviors&gt;&lt;/PropertyGrid&gt;</code>
/// </summary>
/// <remarks>
/// Why anything is needed at all. Items on a canvas are DATA - no property store, no notification - and that is what
/// lets a drawing hold tens of thousands of them. The inspector therefore writes straight into an item and nobody
/// hears it: the model changes and the picture does not. The scene is the thing that already knows how to say "what I
/// would draw has changed", so this connects the one to the other.
/// <para>A BEHAVIOR and not code behind the view, because the view has none by design - and not a change event on every
/// item, because that would put a subscriber list on each of ten thousand strokes to serve the dozen that are ever
/// selected.</para>
/// </remarks>
public class InspectorRedrawBehavior : Behavior<PropertyGrid>
{
    public static readonly AdamantiumProperty SceneProperty = AdamantiumProperty.Register(nameof(Scene),
        typeof(ICanvasScene), typeof(InspectorRedrawBehavior), new PropertyMetadata(null));

    /// <summary>The scene the inspected items belong to.</summary>
    public ICanvasScene Scene
    {
        get => GetValue<ICanvasScene>(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    protected override void OnAttached(PropertyGrid grid) => grid.ValueChanged += OnValueChanged;

    protected override void OnDetached(PropertyGrid grid) => grid.ValueChanged -= OnValueChanged;

    private void OnValueChanged(object sender, PropertyValuesChangedEventArgs e)
    {
        // Whatever was edited, the scene is told once. Which item it was does not matter here: the canvas keeps no copy
        // of the scene, so it simply draws it again.
        if (e.Target is ICanvasItem) Scene?.Touch();
    }
}
