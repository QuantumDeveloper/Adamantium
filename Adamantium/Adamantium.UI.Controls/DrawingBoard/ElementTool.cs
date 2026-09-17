using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Puts a real CONTROL on the plane: drag out where it goes, or click and it takes the size it asks for.
/// <para>What kind of control is a FACTORY and not a type on this class, because the answer belongs to the application:
/// a toolbox of five controls is five of these, and one holding a whole assembled panel is the same tool again.</para>
/// </summary>
public class ElementTool : ICanvasTool
{
    private readonly Func<IUIComponent> _make;

    private Vector2 _from;
    private Rect? _box;

    public ElementTool(Func<IUIComponent> make, Size natural = default)
    {
        _make = make;
        Natural = natural.Width > 0 && natural.Height > 0 ? natural : new Size(140, 32);
    }

    /// <summary>The size a control gets when it is CLICKED rather than dragged out, in SCREEN pixels - the size the hand
    /// asked for by not saying one.</summary>
    public Size Natural { get; }

    public bool IsBusy => false;

    /// <summary>How a rail shows this tool. No default picture and no default key: what control this tool puts down is
    /// the application's choice, so what it is called and how it looks are its choice too.</summary>
    public string Name { get; set; } = "Control";

    public string Icon { get; set; } = string.Empty;

    public Key Shortcut { get; set; } = Key.None;

    public string Description { get; set; } = string.Empty;

    /// <summary>One family by default, because this is the tool that grows without limit: an application willing to put
    /// its own controls on the plane has as many of these as it has controls, and a rail cannot hold them. Set it to
    /// nothing and the tool takes a button of its own like any other.</summary>
    public string Group { get; set; } = "Controls";

    /// <summary>A crosshair - a control is dragged out from an exact corner, like a shape.</summary>
    public Cursor Cursor { get; set; } = Cursors.Crosshair;

    /// <summary>Which mode this tool belongs to. SAID and not guessed from what it makes: asking the factory would mean
    /// building a control to find out, every time a rail is rebuilt, for every tool it holds. The application knows
    /// what it is putting on the plane.</summary>
    public CanvasMode Mode { get; set; } = CanvasMode.Drawing;

    public bool WorksIn(CanvasMode mode) => mode == Mode;

    public void OnPressed(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (e.Button != MouseButtons.Left || canvas.Scene == null || _make == null) return;

        _from = e.World;
        _box = new Rect(_from.X, _from.Y, 0, 0);

        canvas.CaptureMouse();
        e.Handled = true;
    }

    public void OnMoved(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (_box == null) return;

        _box = Between(_from, e.World);
        canvas.InvalidateRender(false);
        e.Handled = true;
    }

    public void OnReleased(InfiniteCanvas canvas, CanvasPointerEventArgs e)
    {
        if (_box == null) return;

        var box = Between(_from, e.World);
        _box = null;
        canvas.ReleaseMouseCapture();

        var element = _make();
        if (element == null)
        {
            canvas.InvalidateRender(false);
            e.Handled = true;
            return;
        }

        var item = new ElementItem(element, box);

        // A press that never moved is a CLICK, and a click drops the control at the size it asks for rather than at
        // nothing.
        var least = canvas.ScreenToWorldLength(3);
        if (box.Width < least || box.Height < least)
        {
            // In SCREEN pixels for an ordinary control - a button dropped at 1:1 and one dropped zoomed right out
            // should look the same size when you are looking at them, and "did not move" is a fact about the hand.
            //
            // In WORLD units for one sized by its own CONTENT. Its width is a floor its content sets, and a floor is a
            // world measurement: scaled by the camera as well, the same node came out a third as wide when made zoomed
            // in and three times as wide when made zoomed out - the same node, different sizes, depending on nothing
            // but when it was made.
            box = item.SizeFollowsContent
                ? new Rect(_from.X, _from.Y, Natural.Width, Natural.Height)
                : new Rect(_from.X, _from.Y,
                    canvas.ScreenToWorldLength(Natural.Width), canvas.ScreenToWorldLength(Natural.Height));

            item.World = box;
        }

        canvas.Scene?.Add(item);
        canvas.Select(item, false);

        canvas.InvalidateRender(false);
        e.Handled = true;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        // Only an outline while it is being dragged out: the control itself does not exist yet, and a picture of one
        // would be a second way of drawing it that has to be kept looking like the first.
        if (_box is not { } box || canvas.SelectionBrush == null) return;

        session.DrawRectangle(null, canvas.ToScreen(box), new Pen(canvas.SelectionBrush));
    }

    public void Cancel(InfiniteCanvas canvas)
    {
        if (_box == null) return;

        _box = null;
        canvas.ReleaseMouseCapture();
        canvas.InvalidateRender(false);
    }

    private static Rect Between(Vector2 from, Vector2 to) =>
        new(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
}
