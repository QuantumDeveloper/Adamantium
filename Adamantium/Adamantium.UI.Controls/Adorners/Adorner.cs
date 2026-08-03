using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// A visual drawn ON TOP of an adorned element by the framework adorner stage - NOT part of the content visual tree, so it
/// never pollutes the user's tree. A full <see cref="TemplatedUIComponent"/> (WPF's Adorner is likewise a FrameworkElement),
/// so an adorner can either draw itself in <c>OnRender</c> (the designer's selection / hover frames) OR carry a
/// <c>ControlTemplate</c> from the theme and host a styled subtree (the drag-drop insertion cue - see
/// <c>DropInsertionIndicator</c>). It renders in the ADORNED element's coordinate space, so its decorations line up on the
/// target: <see cref="RenderParent"/> is the adorned element, and the adorner stage flattens the whole subtree.
/// </summary>
public class Adorner : TemplatedUIComponent
{
    public Adorner()
    {
    }

    public Adorner(IUIComponent adornedElement)
    {
        AdornedElement = adornedElement;
    }

    private IUIComponent _adornedElement;

    /// <summary>The element this adorner decorates; the adorner (and its template) draw in its coordinate space. Setting it
    /// also makes the adorned element the adorner's INHERITANCE parent, so the adorner inherits its DataContext/inherited
    /// values (a themed adorner can bind {Binding}). We wire InheritanceParent directly - NOT AddLogicalChild - because an
    /// adorner is framework chrome themed OUT-OF-BAND by the adorner stage (like a template root, see AddTemplateChild): a
    /// full logical-child join fires SetParent -> ApplyCurrentTheme on every adorner, which rebuilds its template (and
    /// re-subscribes its {ThemeResource}s to the global Theme) on every hover/selection/drag cue. See docs/TREE_MODEL_DESIGN.md.</summary>
    public IUIComponent AdornedElement
    {
        get => _adornedElement;
        set
        {
            if (ReferenceEquals(_adornedElement, value)) return;
            _adornedElement = value;
            InheritanceParent = value as AdamantiumComponent;
        }
    }

    // Stated as the RENDER PARENT, not a WorldTransform override: the render pass composes each component's transform from
    // LocalTransform x RenderParent, so an adorner outside that chain would draw at the window origin. Pointing RenderParent
    // at the adorned element places the adorner - and its arranged template - exactly on its target.
    public override IUIComponent RenderParent => AdornedElement;

    // ...but NOT clipped to that element's box: an adorner exists to draw AROUND its target, and a control whose
    // template clips its own content would otherwise erase the whole decoration. Everything above the target still
    // clips it - see IUIComponent.ClippedByRenderParent.
    public override bool ClippedByRenderParent => false;

    /// <summary>The adorned element's painted rectangle in its OWN local space: a Shape's stroke-aware RenderBounds,
    /// otherwise its arranged box. What an OnRender-drawing adorner (a selection / hover frame) decorates. Virtual
    /// because an adorner may decorate a box OTHER than the element's own - the focus ring stands off it.</summary>
    public virtual Rect AdornedBounds =>
        AdornedElement is Shape shape ? shape.RenderBounds : new Rect(AdornedElement.RenderSize);

    /// <summary>True if this adorner's template should be sized to fill the adorned element's bounds (a frame that wraps its
    /// target). The adorner stage themes it and re-lays it out to <see cref="AdornedBounds"/> each frame. False for a cue
    /// placed at a specific spot (the drop insertion indicator), which the drag engine sizes and positions itself.</summary>
    public virtual bool FillsAdornedBounds => false;

    /// <summary>Set by the adorner stage once it has applied the theme (so a themed frame is templated, not re-themed each
    /// frame). Reset if the adorner is reused for a different element.</summary>
    public bool ThemeApplied { get; set; }
}
