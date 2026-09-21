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
    private Size _lastArrangeSize;

    public TextPresenter()
    {
        // NOTE: clipping is done by the hosting ScrollContentPresenter (it scissors the overflow to the viewport) - we
        // deliberately do NOT set ClipToBounds here.
        Focusable = false;     // focus belongs to the owning TextBox
    }

    // The scroll surface: report the FULL content size (all lines, full width) so the enclosing ScrollViewer's
    // ScrollContentPresenter knows the extent and provides the viewport/scrollbars. It measures us unbounded on the axes
    // it lets scroll (so we report our natural extent) and constrained on a disabled axis (so wrapping fits the viewport);
    // MeasureSurface already interprets an infinite width as "no wrap / full width" and a finite one as the wrap width.
    protected override Size MeasureOverride(Size availableSize)
        => Owner == null ? new Size() : Owner.MeasureSurface(availableSize.Width);

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Re-render only when the SLOT SIZE changes (content/box resized, e.g. the floating-strip toggle), not on a mere
        // reposition (scrolling moves us via the arrange rect's ORIGIN - the same rendered units just shift, no redraw).
        if (finalSize != _lastArrangeSize) { _lastArrangeSize = finalSize; InvalidateRender(false); }
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
        var p = e.GetPosition(this);

        // A repeat click SELECTS rather than places: the word under it, then the lot. Dragging is not the only way to
        // select, and in a box where the drag is spoken for - a NumericUpDown scrubbing its value on that same press -
        // it is the only way left.
        if (e.ClickCount >= 3)
        {
            Owner.SurfaceSelectAll();
            e.Handled = true;
            return;
        }
        if (e.ClickCount == 2)
        {
            Owner.SurfaceSelectWord(p.X, p.Y);
            e.Handled = true;
            return;
        }

        var shift = (Keyboard.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
        Owner.SurfaceMouseDown(p.X, p.Y, shift);
        _selecting = true;
        CaptureMouse();
        e.Handled = true;
    }

    /// <summary>Abandon a selection drag in progress, for an owner that has decided the press means something else (see
    /// <see cref="NumericUpDown"/>'s scrub). Without it the drag stays armed with no way to end - the button-up goes to
    /// whoever took the capture - and the next hover would carry on selecting.</summary>
    internal void CancelSelection()
    {
        _selecting = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
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
