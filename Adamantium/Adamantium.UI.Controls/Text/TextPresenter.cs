using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Text;

/// <summary>
/// The text-rendering surface of a <see cref="TextBoxBase"/> - the <c>PART_TextPresenter</c> in the control template. It
/// is deliberately thin: measuring, rendering (text + caret + selection + placeholder) and mouse hit-testing all defer to
/// its <see cref="Owner"/>, which owns the text/caret/selection state and the caret-following scroll offset. It clips to
/// its bounds so long text scrolls under the border instead of spilling out, and it is NOT focusable (a click focuses the
/// owning text box, not this internal part).
/// </summary>
public sealed class TextPresenter : InputUIComponent
{
    internal TextBoxBase Owner { get; set; }

    private bool _selecting;

    public TextPresenter()
    {
        // NOTE: clipping is done by the template's Border (ClipToBounds=True) around this presenter - we deliberately do
        // NOT set ClipToBounds here (a redundant second clip on this custom surface was making sibling content vanish).
        Focusable = false;     // focus belongs to the owning TextBox
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Owner == null) return new Size();
        var desired = Owner.MeasureSurface(availableSize.Width);
        // Don't force the box to grow to the full content - cap the DESIRED size at the available slot on both axes; the
        // text that doesn't fit scrolls (the owner's caret-follow offsets), it isn't laid out larger than the viewport.
        // When an axis is unconstrained (infinite) the box takes the content size on that axis (auto-grow).
        var width = double.IsInfinity(availableSize.Width) ? desired.Width : System.Math.Min(desired.Width, availableSize.Width);
        var height = double.IsInfinity(availableSize.Height) ? desired.Height : System.Math.Min(desired.Height, availableSize.Height);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRender(IDrawingContext context)
    {
        if (Owner == null) return;
        Owner.RenderSurface(context.ForControl(this), RenderSize);
    }

    // Non-input geometry still needs to be hit for the click-to-place-caret / drag-select gestures.
    public override bool HitTestCore(Vector2 localPoint) => true;

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (Owner == null) return;
        var shift = (Keyboard.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
        var p = e.GetPosition(this);
        Owner.SurfaceMouseDown(p.X, p.Y, shift);
        _selecting = true;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (_selecting) { var p = e.GetPosition(this); Owner?.SurfaceMouseMove(p.X, p.Y); }
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        if (!_selecting) return;
        _selecting = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }
}
