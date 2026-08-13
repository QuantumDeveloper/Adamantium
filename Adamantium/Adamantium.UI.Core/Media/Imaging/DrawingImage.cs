using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Media.Drawings;

namespace Adamantium.UI.Core.Media.Imaging;

/// <summary>A <see cref="Drawing"/> where a picture is expected - <c>&lt;Image Source="{StaticResource Icon}"/&gt;</c>.
/// It has no pixels: it draws ITSELF into whoever shows it (see <see cref="Render"/>), so one icon resource stays sharp
/// at 16px and at 512px and survives a DPI change with nothing to re-bake. This is why it is not a bitmap subclass.</summary>
public class DrawingImage : ImageSource
{
    public static readonly AdamantiumProperty DrawingProperty = AdamantiumProperty.Register(nameof(Drawing),
        typeof(Drawing), typeof(DrawingImage), new PropertyMetadata(null, DrawingChangedCallback));

    /// <summary>The picture. [Content] so AUML writes it as the child element.</summary>
    [Content]
    public Drawing Drawing
    {
        get => GetValue<Drawing>(DrawingProperty);
        set => SetValue(DrawingProperty, value);
    }

    /// <summary>Raised when the drawing, or anything inside it, changes. An element showing this subscribes and
    /// re-renders - the ordinary brush hook-up in the property system only covers brush-valued properties, and a
    /// DrawingImage sits on an ImageSource property.</summary>
    public event EventHandler Changed;

    private static void DrawingChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not DrawingImage image) return;

        if (e.OldValue is Drawing oldDrawing)
        {
            oldDrawing.Changed -= image.OnDrawingChanged;
        }

        if (e.NewValue is Drawing newDrawing)
        {
            newDrawing.Changed += image.OnDrawingChanged;
        }

        // A drawing swapped in later must be hung on the same owner the image already has.
        if (image._owner != null)
        {
            image._dataContext = null;   // force the re-attach below to actually run for the new drawing
            image.Attach(image._owner);
        }

        image.OnDrawingChanged(a, EventArgs.Empty);
    }

    private void OnDrawingChanged(object sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Hang this image, and the whole drawing under it, on the component that shows it, so bindings written
    /// inside the drawing resolve against that component's DataContext. Called by the consumer when it takes the
    /// source. Without it every <c>{Binding}</c> inside a drawing RESOURCE yields null and the picture is blank -
    /// which for a brush means drawing nothing at all, silently.</summary>
    public void Attach(AdamantiumComponent owner)
    {
        // Called from the point of USE (measure/render), not from a lifecycle event, and this is why: at the moment an
        // element is attached to the tree its ancestors' inheritance chain is not finished yet - the walk up from the
        // element stops two panels short and reaches no DataContext at all. There is no later notification either,
        // because an INHERITED DataContext arriving raises nothing per descendant. By the time anyone measures or draws
        // the drawing, the tree IS built. Re-attaching only when the owner or its data actually changed keeps that
        // cheap: after the first successful bind every later call returns immediately.
        var dataContext = (owner as IUIComponent)?.DataContext;

        // One inheritance parent, so the FIRST owner that can answer keeps it - a later one would move where every
        // binding inside the drawing resolves.
        if (_owner != null && _dataContext != null && !ReferenceEquals(_owner, owner))
        {
            return;
        }

        if (ReferenceEquals(_owner, owner) && ReferenceEquals(_dataContext, dataContext))
        {
            return;
        }

        _owner = owner;
        _dataContext = dataContext;
        InheritanceParent = owner;
        Drawing?.Attach(this);
    }

    private AdamantiumComponent _owner;

    private object _dataContext;

    /// <summary>The drawing's own extent. This is what a consumer measures against, exactly as it would a bitmap's pixel
    /// size - so Stretch, alignment and the fitted destination rect all keep working unchanged.</summary>
    public override double Width => Drawing?.Bounds.Width ?? 0;

    public override double Height => Drawing?.Bounds.Height ?? 0;

    /// <summary>Replay into <paramref name="destination"/>. The drawing's own bounds are its viewbox: they map onto the
    /// destination rect, so an icon authored in any coordinate box lands correctly without the author converting
    /// anything. Non-uniform on purpose - the caller (Image.Stretch) has already decided the aspect.</summary>
    public void Render(IDrawingSession session, Rect destination)
    {
        var drawing = Drawing;
        if (drawing == null) return;

        var viewbox = drawing.Bounds;
        if (viewbox.Width <= 0 || viewbox.Height <= 0) return;

        var scaleX = (float)(destination.Width / viewbox.Width);
        var scaleY = (float)(destination.Height / viewbox.Height);

        // Move the viewbox origin to zero BEFORE scaling: an icon whose shapes start at (8,8) must not keep that gap
        // scaled up with it, the same way a shape's own leading gap would otherwise be baked into its size.
        var transform = Matrix4x4F.Translation((float)-viewbox.X, (float)-viewbox.Y, 0) *
                        Matrix4x4F.Scaling(scaleX, scaleY, 1) *
                        Matrix4x4F.Translation((float)destination.X, (float)destination.Y, 0);

        drawing.Render(session, transform);
    }
}
