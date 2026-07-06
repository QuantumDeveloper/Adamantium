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
        // Cap the DESIRED size at the slot ONLY on an axis the owner FIXED (explicit Width/Height): there the box can't
        // grow, so content that doesn't fit scrolls (the owner's caret-follow offsets). On an AUTO axis report the full
        // desired so the box grows to fit - capping there against a MinWidth/Height-derived slot would trap the box at
        // that minimum (the floating-strip toggle then reserved the strip but the box never grew, clipping the text).
        var width = !double.IsNaN(Owner.Width) && !double.IsInfinity(availableSize.Width)
            ? System.Math.Min(desired.Width, availableSize.Width) : desired.Width;
        var height = !double.IsNaN(Owner.Height) && !double.IsInfinity(availableSize.Height)
            ? System.Math.Min(desired.Height, availableSize.Height) : desired.Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Re-render on a (re-)arrange: the owner's RenderSurface positions text/caret/label against this size, so when the
        // slot changes (e.g. the floating-label strip toggles the box height a frame after the render already ran with the
        // stale size) it must redraw against the NEW size - otherwise the strip pushes the text below the old height and
        // clips it. Arrange self-gates on the rect, so this only fires when the size actually changed.
        InvalidateRender(false);
        return finalSize;
    }

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
