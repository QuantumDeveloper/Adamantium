using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Rendering;

/// <summary>
/// A minimal off-screen <see cref="IRootVisualComponent"/> that hosts a single visual so <see cref="VisualRenderer"/> can
/// record + render it into a texture (the engine's analog of UWP's RenderTargetBitmap root). It is NOT an OS window: the
/// screen<->client transforms are identity and there is no UI context. It measures/arranges its content to the requested
/// size, and <see cref="RootVisualExtensions.GetProjectionMatrix"/> reads ClientWidth/ClientHeight for the projection.
///
/// Hosting adds the content as this root's visual child, so it is for FRESH/detached trees (AUML-loaded or not-in-a-window
/// visuals). A live, already-parented on-screen element must NOT be hosted here (two parents) - that case is baked through
/// a parallel render cache without reparenting (see DRAG_DROP_PLAN Phase 1).
/// </summary>
internal sealed class VisualRoot : MeasurableUIComponent, IRootVisualComponent
{
    private readonly IUIComponent _content;

    public VisualRoot(IUIComponent content, double width, double height)
    {
        ClientWidth = width;
        ClientHeight = height;
        _content = content;
        if (_content != null) AddVisualChild(_content);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_content is IMeasurableComponent measurable) measurable.Measure(availableSize);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_content is IMeasurableComponent measurable) measurable.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public double Left { get; set; }
    public double Top { get; set; }
    public string Title { get; set; }
    public double ClientWidth { get; set; }
    public double ClientHeight { get; set; }

    // Off-screen: no OS window, no context, identity screen<->client (the projection maps client space 1:1).
    public IUIContext UIContext => null;
    public void AttachContextAndInitialize(IUIContext context) { }
    // Identity: with no OS window there is no display scale to cross, so the two units coincide here.
    public PixelPoint Position { get; set; }
    public Vector2 PointToClient(PixelPoint point) => new((float)point.X, (float)point.Y);
    public PixelPoint PointToScreen(Vector2 point) => new(point.X, point.Y);
}
