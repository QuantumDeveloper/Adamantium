using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A real CONTROL on the plane - a button, a text box, a panel of them - at a place and a size in the world.
/// <para>The bridge the other items are not: a control has a template, takes input and edits itself, and none of that
/// has to be rebuilt here to put one on a canvas. It costs a node in the visual tree and a property store, which is
/// exactly why ink is NOT one of these - a drawing holds tens of thousands of strokes and a board holds dozens of
/// controls.</para>
/// <para>Zoom costs it nothing: it is laid out once per size change, and panning is a new arrange of a rectangle, not a
/// re-measure of what is inside it.</para></summary>
public class ElementItem : ICanvasItem, ICanvasTransformed
{
    private Boolean? _sizeFollowsContent;
    private Rect _world;

    public ElementItem(IUIComponent element, Rect world)
    {
        Element = element;
        World = world;
    }

    /// <summary>The control itself. It lives in the canvas's own visual tree while it is on screen, so it draws, takes
    /// input and animates the way it would anywhere else.</summary>
    public IUIComponent Element { get; }

    /// <summary>THE CONTROL A PERSON SEES - which is not always what is hosted.
    /// <para>An application's own object is put on the plane as a <see cref="ContentPresenter"/> with the object's
    /// template inside it, so the button somebody points at and recolours is the presenter's CHILD. Written on the
    /// presenter, a background is painted under an opaque button and a corner radius belongs to something that has no
    /// corners: the panel's lines read a value, wrote one, and nothing on screen moved - except the foreground, which
    /// is inherited and reached the text by itself.</para>
    /// <para>For a control put down by a tool the two are the same thing.</para></summary>
    public IUIComponent Painted
    {
        get
        {
            if (Element is not ContentPresenter { VisualChildren.Count: 1 } presenter) return Element;

            foreach (var child in presenter.VisualChildren) return child;

            return Element;
        }
    }

    /// <summary>WHAT THIS STANDS FOR, when the canvas made it for a node of the application's own graph - null for a
    /// control somebody put on a drawing.
    /// <para>A container knows what it is showing, the way a list's row knows its item: an inspector pointed at the
    /// selection has to reach the node's own object, not the control drawn for it, or what it edits is the picture.
    /// </para></summary>
    public ICanvasPlaced Model { get; internal set; }

    /// <summary>Where it sits on the plane, in WORLD units - so it grows with the zoom like everything else drawn on the
    /// canvas, rather than staying a fixed number of pixels the way the grips do.
    /// <para>For a container standing for a node, this is the NODE's own place and not a copy of it: read and written
    /// straight through the model, so a node dragged on the plane and a node moved from a field are one thing happening,
    /// not two that somebody has to keep equal. The size is the measured one, which is the layer's to know.</para>
    /// </summary>
    public Rect World
    {
        // NEVER NARROWER THAN WHAT IS IN IT, whoever asked. A grip is not the only way a width is written down - an
        // inspector line edits it and a file carries it - so the floor stands where the width is READ rather than at
        // each of the places it can be set, which is where one of them would eventually be forgotten.
        get => Model == null
            ? _world
            : new Rect(Model.Left, Model.Top,
                Math.Max(Model.Width > 0 ? Model.Width : _world.Width, Smallest.Width), _world.Height);
        set
        {
            _world = value;

            if (Model == null) return;

            Model.Left = value.X;
            Model.Top = value.Y;
        }
    }

    /// <summary>Whether the CONTROL decides the box rather than the box deciding the control.
    /// <para>Not the same rule in both directions, because the two are not the same question. The height is EXACTLY
    /// what the control asks for: a node is as tall as its sockets and there is no such thing as a node with room to
    /// spare under them, so dragging the bottom edge has nothing to set. The width is a FLOOR: a node may well be
    /// dragged wider - long names, and room to read them - but narrower than its own labels it simply spills out of its
    /// frame, which is what a box smaller than its control always does.</para>
    /// <para>Most controls do neither: a button dragged out to fifty pixels is a fifty-pixel button, and one that
    /// resized itself under the hand would be unusable. Default, not law - an application with a control of its own
    /// that behaves like a node says so here.</para></summary>
    public Boolean SizeFollowsContent
    {
        get => _sizeFollowsContent ?? Element is CanvasNode;
        set => _sizeFollowsContent = value;
    }

    public Rect Bounds => World;

    /// <summary>The LEAST it may be pulled to, in world units.
    /// <para>For something whose size FOLLOWS ITS CONTENT - a node - this is written down by the layer that measured
    /// it, because the measure is the one place what it needs is known, and anything set here is replaced.</para>
    /// <para>For a control placed at a size somebody chose it is left alone and starts as nothing, which is what
    /// choosing a size means: pull it to whatever you like. An application that knows better - "this panel is useless
    /// under two hundred by eighty" - says so here.</para></summary>
    public Size Smallest { get; set; }

    /// <summary>A second one of whatever is hosted here - but only when the engine can honestly make one.
    /// <para>A NODE it can: a node is the engine's own control and everything that makes one what it is - its title,
    /// its accent, its sockets with their names, colours and kinds - is readable and settable. Anything else is the
    /// APPLICATION's control, and there is no way to make a second of something the engine has never seen: null, which
    /// the copier reads as "leave this one behind" rather than pasting a broken half.</para>
    /// <para>What is INSIDE the node is not copied either, for the same reason: it is the application's. A copied node
    /// comes back with its frame and its sockets, and whoever put a field in it puts one in the copy.</para></summary>
    public ICanvasItem Copy()
    {
        if (Element is not CanvasNode node) return null;

        var made = new CanvasNode
        {
            Kind = node.Kind,
            Title = node.Title,
            Accent = node.Accent,
            PinColor = node.PinColor,
            IsCollapsed = node.IsCollapsed,
            Inputs = node.InputPins.Count,
            Outputs = node.OutputPins.Count
        };

        Dress(node.InputPins, made.InputPins);
        Dress(node.OutputPins, made.OutputPins);

        return new ElementItem(made, World) { SizeFollowsContent = SizeFollowsContent };
    }

    private static void Dress(System.Collections.ObjectModel.ObservableCollection<CanvasNodePin> from,
        System.Collections.ObjectModel.ObservableCollection<CanvasNodePin> to)
    {
        for (var i = 0; i < from.Count && i < to.Count; i++)
        {
            to[i].Name = from[i].Name;
            to[i].Color = from[i].Color;
            to[i].Kind = from[i].Kind;
        }
    }

    /// <summary>A NODE belongs to a graph; every other control on the plane - a button, a field, a check box - is part
    /// of the drawing it was put on. Decided by what is being hosted and not by a flag on the item: which of the two a
    /// control is, is a fact about the control.</summary>
    public CanvasMode Mode => Element is CanvasNode ? CanvasMode.Nodes : CanvasMode.Drawing;

    /// <summary>The control's own type, and what it says if it says anything: a page holding six buttons needs to tell
    /// them apart, and the only thing that does is the words on them.</summary>
    public string Title
    {
        get
        {
            var kind = Element?.GetType().Name ?? "Element";
            var says = Label;

            return string.IsNullOrWhiteSpace(says) ? kind : $"{kind} \"{says}\"";
        }
    }

    /// <summary>Where and how big, one number at a time. <see cref="World"/> is a rectangle and a rectangle cannot be
    /// half-written, so an inspector line that edits only the X of one has nothing to bind to - these are that line.
    /// </summary>
    public Double X
    {
        get => World.X;
        set => World = new Rect(value, World.Y, World.Width, World.Height);
    }

    public Double Y
    {
        get => World.Y;
        set => World = new Rect(World.X, value, World.Width, World.Height);
    }

    public Double Width
    {
        get => World.Width;
        set => World = new Rect(World.X, World.Y, Math.Max(1, value), World.Height);
    }

    public Double Height
    {
        get => World.Height;
        set => World = new Rect(World.X, World.Y, World.Width, Math.Max(1, value));
    }

    /// <summary>How round each of the control's corners is, one at a time, and how thick its border is - one number for
    /// all four sides there, because a border of three different widths is not what anybody is reaching for on a plane.
    /// <para>Here for the same reason <see cref="X"/> is: a corner radius and a thickness are STRUCTS, and a struct
    /// cannot be written half at a time, so an inspector line editing one corner has nothing to bind to.</para>
    /// <para>Zero for a control that has no such property at all - a panel has no border to thicken.</para></summary>
    /// <summary>The corners of whatever is being shown - ASKED BY NAME, the way a binding asks.
    /// <para>A button, a border, a rectangle and a picture all round their corners, and none of them inherits the
    /// property from the others. Listing the types here would be a list to extend for the next one, and an interface
    /// over them would be a second answer to a question the property system already answers: a component knows its own
    /// properties by name, which is exactly what {Binding CornerRadius} uses.</para>
    /// <para>The four corners are separate lines because a CornerRadius is a STRUCT - it cannot be written a quarter at
    /// a time - which is the one thing a binding cannot do for us.</para></summary>
    private CornerRadius Corners
    {
        get => Rounded(out var component, out var property) ? (CornerRadius)component.GetValue(property) : default;
        set
        {
            if (Rounded(out var component, out var property)) component.SetValue(property, value);
        }
    }

    private bool Rounded(out IAdamantiumComponent component, out AdamantiumProperty property)
    {
        component = Painted as IAdamantiumComponent;
        property = component?.GetProperty(nameof(Control.CornerRadius));

        return property != null && property.PropertyType == typeof(CornerRadius);
    }

    public Double CornerTopLeft
    {
        get => Corners.TopLeft;
        set => SetCorner(value, Corners, 0);
    }

    public Double CornerTopRight
    {
        get => Corners.TopRight;
        set => SetCorner(value, Corners, 1);
    }

    public Double CornerBottomRight
    {
        get => Corners.BottomRight;
        set => SetCorner(value, Corners, 2);
    }

    public Double CornerBottomLeft
    {
        get => Corners.BottomLeft;
        set => SetCorner(value, Corners, 3);
    }


    public Double BorderWidth
    {
        get => Painted is Control control ? control.BorderThickness.Left : 0;
        set
        {
            if (Painted is Control control) control.BorderThickness = new Thickness(Math.Max(0, value));
        }
    }

    /// <summary>What the control SAYS - its content if it has content, its text if it is a box, the strip across its top
    /// if it is a node.
    /// <para>Here rather than "inspect the control itself", because the thing selected on the plane is this item, and an
    /// inspector line binds against what is selected. A control that says nothing answers with nothing and takes
    /// nothing: a panel has no label and pretending it has one would only offer a line that does not work.</para>
    /// </summary>
    public String Label
    {
        // THE CONTROL, not the host it stands in: an application's object is hosted inside a ContentPresenter, and
        // that presenter's content is the application's OBJECT - so a label read off it was the object's ToString and
        // a label written onto it replaced the object with a string. What a person means by "what it says" is what the
        // button says. See Painted.
        get => Painted switch
        {
            TextBox box => box.Text,
            // Before the content control: a node is not one, so without this it answered nothing and read in the
            // structure list as a nameless "CanvasNode" - in a graph of fifty, fifty times over.
            CanvasNode node => node.Title?.ToString(),
            IContentControl content => content.Content?.ToString(),
            _ => null
        };
        set
        {
            switch (Painted)
            {
                case TextBox box:
                    box.Text = value;
                    break;

                case CanvasNode node:
                    node.Title = value;
                    break;

                case IContentControl content:
                    content.Content = value;
                    break;
            }
        }
    }

    private Vector2 Middle => new(World.X + World.Width / 2, World.Y + World.Height / 2);

    private CanvasTransform _transform = CanvasTransform.None;

    /// <summary>The turn and the lean this control is drawn with - the same statement a shape carries, and about the
    /// middle of its own box for the same reason.
    /// <para>Carried out by the LAYER, in the one render transform it already puts on every hosted control for the
    /// zoom: a second transform written here replaced that one, and the control then stood at its own size inside a
    /// frame drawn at the camera's - which is a control and its frame walking away from each other.</para>
    /// <para>A render transform and not a layout one: layout is what the item's box says, and a control that laid
    /// itself out turned would change size as it turned. Hit-testing follows a render transform, so a turned control is
    /// still pressed where it is seen.</para></summary>
    public CanvasTransform Transform
    {
        get => _transform;
        set => _transform = value;
    }

    public double Angle
    {
        get => Transform.Angle;
        set => Transform = Transform with { Angle = value };
    }

    public double SkewX
    {
        get => Transform.SkewX;
        set => Transform = Transform with { SkewX = value };
    }

    public double SkewY
    {
        get => Transform.SkewY;
        set => Transform = Transform with { SkewY = value };
    }

    public void Move(Vector2 worldDelta) =>
        World = new Rect(World.X + worldDelta.X, World.Y + worldDelta.Y, World.Width, World.Height);

    public void Resize(Rect world)
    {
        if (world.Width <= 0 || world.Height <= 0) return;

        // NEVER UNDER WHAT IS INSIDE IT. The layer already refuses to MEASURE a node narrower than its own contents,
        // but for a node that width is kept on the model and the layer's correction never reached it - so a grip could
        // write a width of nothing onto the node and the node was drawn spilling out of a frame two pixels wide.
        var width = Math.Max(world.Width, Smallest.Width);
        var height = Math.Max(world.Height, Smallest.Height);

        World = new Rect(world.X, world.Y, width, height);

        // A width ASKED FOR, which the measured one is not: the hand on the edge is the only thing that says a node is
        // to be wider than it needs, so it is the only thing that writes it down.
        if (Model != null) Model.Width = width;
    }

    public bool HitTest(Vector2 world, double tolerance)
    {
        // TURNED BACK FIRST, the way a shape does it: the box is stated straight, so a point is asked about in the
        // control's own frame rather than the frame it is drawn in.
        if (_transform.IsSomething) world = _transform.Undo(world, Middle);

        return world.X >= World.X - tolerance && world.X <= World.X + World.Width + tolerance &&
               world.Y >= World.Y - tolerance && world.Y <= World.Y + World.Height + tolerance;
    }

    /// <summary>Nothing: a control draws ITSELF, from its own template, as a child of the canvas. Everything else on the
    /// plane is data and has to be painted here; this one is the case that is not.</summary>
    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
    }

    /// <summary>ONE TILE's rectangle, as a fraction of the thing it paints - the four numbers that decide how big a
    /// repeat is and where the first copy starts. A quarter across and a quarter down is sixteen copies.
    /// <para>Here for the same reason the corners are: a Rect is a STRUCT, so a line editing one side of it has
    /// nothing to bind to. Everything else about a texture - how it fits, whether it repeats, which way up, its turn,
    /// its tint - is a property of its own and is bound straight through, with nothing added here.</para>
    /// <para>Zero-sized where the thing is not painted with a picture at all: there is no tile to speak of, and a
    /// panel showing four numbers about nothing is a panel telling a story.</para></summary>
    public Double TileX
    {
        get => Tiled?.Viewport.X ?? 0;
        set => SetTile(value, 0);
    }

    public Double TileY
    {
        get => Tiled?.Viewport.Y ?? 0;
        set => SetTile(value, 1);
    }

    public Double TileWidth
    {
        get => Tiled?.Viewport.Width ?? 0;
        set => SetTile(value, 2);
    }

    public Double TileHeight
    {
        get => Tiled?.Viewport.Height ?? 0;
        set => SetTile(value, 3);
    }

    /// <summary>The thing being shown, WHERE IT SHOWS A PICTURE - which is what the lines about a texture are about.
    /// Not "is this a texture": a texture is an ordinary picture control put on the plane, and a picture put there any
    /// other way has the same questions to answer.</summary>
    public Image Tiled => Painted as Image;

    private void SetTile(double value, int side)
    {
        if (Tiled is not { } brush) return;

        var box = brush.Viewport;

        // A tile of no width is a picture that never lands, and the brush would go on being asked for copies of
        // nothing. The smallest tile is one pixel's worth of the shape, which is as small as anybody means.
        brush.Viewport = side switch
        {
            0 => new Rect(value, box.Y, box.Width, box.Height),
            1 => new Rect(box.X, value, box.Width, box.Height),
            2 => new Rect(box.X, box.Y, Math.Max(0.001, value), box.Height),
            _ => new Rect(box.X, box.Y, box.Width, Math.Max(0.001, value))
        };
    }

    private void SetCorner(double value, CornerRadius current, int corner)
    {
        value = Math.Max(0, value);
        Corners = corner switch
        {
            0 => new CornerRadius(value, current.TopRight, current.BottomRight, current.BottomLeft),
            1 => new CornerRadius(current.TopLeft, value, current.BottomRight, current.BottomLeft),
            2 => new CornerRadius(current.TopLeft, current.TopRight, value, current.BottomLeft),
            _ => new CornerRadius(current.TopLeft, current.TopRight, current.BottomRight, value)
        };
    }
}
