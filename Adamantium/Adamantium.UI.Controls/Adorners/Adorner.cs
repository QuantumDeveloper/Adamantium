using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// Base for a tooling visual drawn ON TOP of an adorned element by the framework adorner stage (it is NOT part of the
/// content visual tree). The adorner renders in the adorned element's coordinate space - WorldTransform returns the
/// target's - so decorations drawn at the target's bounds line up with it exactly without the adorner being laid out.
/// This is the framework equivalent of WPF's Adorner; the designer uses it for selection frames.
/// </summary>
public abstract class Adorner : UIComponent
{
    protected Adorner(UIComponent adornedElement)
    {
        AdornedElement = adornedElement;
    }

    /// <summary>The element this adorner decorates; the adorner draws in its coordinate space.</summary>
    public UIComponent AdornedElement { get; }

    // Render in the adorned element's space so decorations align exactly (the adorner itself is never measured/arranged,
    // so its own LocalTransform is the identity and WorldTransform composes to exactly the adorned element's).
    //
    // This is stated as the RENDER PARENT, not as a WorldTransform override, because the render pass does NOT read
    // WorldTransform: it composes each component's transform frame-scoped from the frozen snapshot (LocalTransform x
    // parent), and an override outside that chain is simply invisible to it - the adorner then drew at the window ORIGIN
    // instead of on its target (only ever right for an element that happens to sit at 0,0).
    public override IUIComponent RenderParent => AdornedElement;

    /// <summary>The adorned element's painted rectangle in its OWN local space: a Shape's stroke-aware RenderBounds,
    /// otherwise its arranged box. This is what decorations frame.</summary>
    protected Rect AdornedBounds =>
        AdornedElement is Shape shape ? shape.RenderBounds : new Rect(AdornedElement.RenderSize);
}
