using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Core.Media.Drawings;

/// <summary>A RETAINED, resolution-independent picture: a description of draw calls rather than pixels. Unlike a bitmap
/// it is replayed straight into whatever session is drawing it (see <see cref="Render"/>), so the same drawing used at
/// 16px and at 512px is one shared mesh drawn twice - crisp at both, and nothing to re-bake when a size or the DPI
/// changes. That is the whole reason it exists next to <see cref="Imaging.ImageSource"/>.</summary>
public abstract class Drawing : AdamantiumComponent
{
    protected Drawing()
    {
        PropertyChanged += (_, _) => RaiseChanged();
    }

    /// <summary>Raised when anything about the picture changes - a property here, a nested child, or a brush/geometry one
    /// of them draws with. Whoever shows the drawing subscribes and re-renders; without it a change buried three levels
    /// down inside a <see cref="DrawingGroup"/> would never reach the element holding the outermost drawing.</summary>
    public event EventHandler Changed;

    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Bubble a nested drawing's change out as this one's own.</summary>
    protected void OnChildChanged(object sender, EventArgs e) => RaiseChanged();

    /// <summary>The picture's extent in ITS OWN coordinates. This is the viewbox a consumer maps onto its destination,
    /// so an icon authored in a 0..24 box scales by destination/24 - the drawing itself never knows its output size.</summary>
    public abstract Rect Bounds { get; }

    /// <summary>Replay into <paramref name="session"/>, placing this drawing through <paramref name="transform"/>
    /// (the consumer's viewbox-to-destination mapping, with any enclosing group transforms already folded in).</summary>
    public abstract void Render(IDrawingSession session, Matrix4x4F transform);

    /// <summary>Hang this drawing, and everything inside it, under the component that shows it. A drawing lives in a
    /// RESOURCE - outside the tree entirely - so on its own a <c>{Binding}</c> anywhere inside has no DataContext to
    /// resolve against and silently produces null: a drawing whose brushes bind to the view model then draws NOTHING.
    /// The inheritance parent is the same route <see cref="Transform"/> takes for exactly the same reason.</summary>
    public void Attach(AdamantiumComponent parent)
    {
        InheritanceParent = parent;
        // A resource is BUILT before it is ever shown, so its bindings were established with no parent and nothing else
        // would re-run them - a drawing has no attach event of its own (again as Transform).
        BindingEngine.RefreshBindings(this);
        AttachChildren();
    }

    /// <summary>Pass the attachment on to whatever this drawing owns - nested drawings, brushes, transforms.</summary>
    protected virtual void AttachChildren() { }

    /// <summary>Hang one owned component (a brush, a geometry) under this drawing and re-establish its bindings.</summary>
    protected void AttachOwned(AdamantiumComponent owned)
    {
        if (owned == null) return;

        owned.InheritanceParent = this;
        BindingEngine.RefreshBindings(owned);
    }
}
