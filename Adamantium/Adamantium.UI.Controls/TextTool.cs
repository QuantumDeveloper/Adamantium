using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>Words. A click puts a caret on the plane and what is typed appears there; clicking an existing piece of text
/// puts the caret back into it, and Escape - or a click anywhere else - finishes.
/// <para>A new piece goes into the scene when the caret LEAVES it, not while it is being typed - the same rule the pen
/// follows, and for the same reason: half a label is not something to hit-test, save or undo. A caret nobody typed into
/// leaves nothing behind at all, and emptying an existing piece takes it out.</para></summary>
public class TextTool : ICanvasTool
{
    // The SAME cadence a text box blinks at. A caret that sat still read as a line somebody had drawn; blinking is what
    // says "type here", and it has to say it in the one rhythm every caret on the machine uses.
    private const double BlinkSeconds = 0.53;

    private TextItem _editing;
    private bool _fresh;
    private bool _blinking;
    private bool _caretVisible = true;
    private double _blinkAccum;

    /// <summary>A caret outlives the click that placed it - which is exactly what this asks.</summary>
    public bool IsBusy => _editing != null;

    public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (e.Button != MouseButtons.Left || canvas.Scene == null) return;

        Finish(canvas);

        // On an existing piece the caret goes back INTO it - the same click that would have started a new one, because
        // there is nothing else clicking on words could mean.
        if (Existing(canvas, e.Pointer) is { } found)
        {
            _editing = found;
            _fresh = false;
        }
        else
        {
            _editing = new TextItem(e.World, string.Empty, canvas.Ink,
                canvas.ScreenToWorldLength(canvas.TextSize));
            _fresh = true;
        }

        canvas.Focus();
        StartBlink(canvas);
        canvas.InvalidateRender(false);
        e.Handled = true;
    }

    private void StartBlink(InfiniteCanvas canvas)
    {
        _caretVisible = true;
        _blinkAccum = 0;

        if (_blinking) return;
        _blinking = true;

        AnimationManager.AddTicker(dt =>
        {
            // The ticker stops itself when there is nothing left to blink for - a caret is the only reason it is
            // running, and one that outlived its caret would repaint the canvas forever.
            if (_editing == null)
            {
                _blinking = false;
                return true;
            }

            _blinkAccum += dt;
            if (_blinkAccum < BlinkSeconds) return false;

            _blinkAccum -= BlinkSeconds;
            _caretVisible = !_caretVisible;
            canvas.InvalidateRender(false);

            return false;
        });
    }

    public void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
    }

    public void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
    }

    public void OnKey(InfiniteCanvas canvas, KeyEventArgs e)
    {
        if (_editing == null) return;

        switch (e.Key)
        {
            case Key.Escape:
                Finish(canvas);
                e.Handled = true;
                return;

            case Key.BackSpace when _editing.Text.Length > 0:
                _editing.Text = _editing.Text[..^1];
                break;

            // Enter is a NEWLINE and not a way out: a caption runs to two lines more often than it is finished by
            // pressing return, and Escape is already the way out.
            case Key.Enter:
                _editing.Text += "\n";
                break;

            default:
                return;
        }

        Touch(canvas);
        e.Handled = true;
    }

    public void OnText(InfiniteCanvas canvas, TextInputEventArgs e)
    {
        if (_editing == null || string.IsNullOrEmpty(e.Text)) return;

        // Control characters arrive here too - the backspace and the return the key handler already answered. Taking
        // them as text would append a glyph nobody can see and a deletion nobody asked for.
        foreach (var character in e.Text)
        {
            if (!char.IsControl(character)) _editing.Text += character;
        }

        Touch(canvas);
        e.Handled = true;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (_editing == null) return;

        // A caret, and the piece itself while it is still too new to be in the scene. Drawn at the END of the text, in
        // SCREEN pixels: it is a mark about where typing goes, not part of the drawing.
        if (_fresh) _editing.Render(session, canvas);

        if (!_caretVisible || canvas.SelectionBrush == null) return;

        // As tall as the LINE, not as tall as the measured box: the box is the text area the layout reports, and for one
        // short line that is a good deal taller than the letters - a caret drawn to it stood far above and below them
        // and read as a frame rather than as a place to type.
        var box = _editing.Bounds;
        var at = canvas.WorldToScreen(new Vector2(box.X + box.Width, box.Y));
        var height = Math.Max(2, _editing.LineHeight * canvas.Scale);

        session.DrawRectangle(canvas.SelectionBrush, new Rect(at.X, at.Y, 1.5, height));
    }

    public void Cancel(InfiniteCanvas canvas) => Finish(canvas);

    // What was typed goes in; an empty caret leaves nothing behind.
    private void Finish(InfiniteCanvas canvas)
    {
        var finished = _editing;
        _editing = null;

        if (finished == null) return;

        if (_fresh && finished.Text.Length > 0) canvas.Scene?.Add(finished);
        else if (!_fresh && finished.Text.Length == 0) canvas.Scene?.Remove(finished);

        canvas.InvalidateRender(false);
    }

    // A piece that is already in the scene has to be told it changed - the scene cannot notice, because what an item
    // holds is the item's business. One that is not in it yet has nothing to tell, only something to redraw.
    private void Touch(InfiniteCanvas canvas)
    {
        if (!_fresh) canvas.Scene?.Touch();
        canvas.InvalidateRender(false);
    }

    private static TextItem Existing(InfiniteCanvas canvas, Vector2 world)
    {
        if (canvas.Scene == null) return null;

        var reach = canvas.ScreenToWorldLength(4);
        var probe = new Rect(world.X - reach, world.Y - reach, reach * 2, reach * 2);

        TextItem found = null;
        foreach (var item in canvas.Scene.ItemsIn(probe))
        {
            if (item is TextItem text && text.HitTest(world, reach)) found = text;
        }

        return found;
    }
}
