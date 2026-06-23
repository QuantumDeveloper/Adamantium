using System;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

public class ForwardWindowRenderer : WindowRendererBase
{
    private readonly RenderCache _renderCache;
    public ForwardWindowRenderer(IGraphicsDevice device, IRenderUnitFactory renderUnitFactory) : base(device, renderUnitFactory)
    {
        _renderCache = new RenderCache(DrawingContext, renderUnitFactory);
    }
        
    private void OnClientSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowResources();
    }

    private void UpdateWindowResources()
    {
        IsRendererUpToDate = false;
        InitializeWindowResources();
    }

    protected override void InitializeWindowResources()
    {
        // Viewport/scissor at the render resolution (ClientSize x RenderScale); the projection stays the logical
        // ClientSize, so RenderScale > 1 rasterises the same layout into a larger target (crisp designer zoom).
        var width = (uint)(Window.ClientWidth * RenderScale);
        var height = (uint)(Window.ClientHeight * RenderScale);

        Viewport.Width = width;
        Viewport.Height = height;

        Scissor.Extent = new Extent2D();
        Scissor.Extent.Width = width;
        Scissor.Extent.Height = height;
        Scissor.Offset = new Offset2D();
        
        Parameters.Width = width;
        Parameters.Height = height;
        base.InitializeWindowResources();
    }
    
    protected override void UnsubscribeFromEvents()
    {
        if (Window == null) return;
        
        Window.ClientSizeChanged -= OnClientSizeChanged;
        Window.MSAALevelChanged -= OnMSAALevelChanged;
    }

    protected override void SubscribeToEvents()
    {
        Window.ClientSizeChanged += OnClientSizeChanged;
        Window.MSAALevelChanged += OnMSAALevelChanged;
    }

    private void OnMSAALevelChanged(object sender, MSAALevelChangedEventArgs e)
    {
        UpdateWindowResources();
    }

    public override void Render(AppTime appTime)
    {
        if (Window == null) return;

        GraphicsDevice.SetViewports(Viewport);
        GraphicsDevice.SetScissors(Scissor);
        _renderCache.Render();
    }

    public override void PreRender()
    {
        if (Window == null) return;
        _renderCache.PreRender();
    }

    public override void PrepareData()
    {
        if (Window == null) return;

        _renderCache.BuildFromVisualTree(Window);
        _renderCache.ProcessCommands(Window.GetProjectionMatrix());
    }

    // Headless designer: each render is a fresh tree (new RenderIds), so drop the cached units between renders instead
    // of relying on attachment-based reconciliation. Caller must ensure the GPU is idle first (the designer waits).
    public override void ResetCache() => _renderCache.DisposeUnits();

    private void RenderComponent(IUIComponent component)
    {
        if (component.Visibility != Visibility.Visible) return;

        //if (!DrawingContext.GetContainerForComponent(component, out var renderContainer)) return;

        // if (component.ClipToBounds)
        // {
        //     var clipRect = new Rect2D();
        //     clipRect.Offset = new Offset2D();
        //     clipRect.Offset.X = (int)component.ClipRectangle.X;
        //     clipRect.Offset.Y = (int)component.ClipRectangle.Y;
        //     clipRect.Extent = new Extent2D();
        //     clipRect.Extent.Width = (uint)component.ClipRectangle.Width;
        //     clipRect.Extent.Height = (uint)component.ClipRectangle.Height;
        //
        //     GraphicsDevice.SetScissors(clipRect);
        // }
        // else
        {
            GraphicsDevice.SetScissors(Scissor);
        }
        
        //renderContainer.Draw(GraphicsDevice, component, ProjectionMatrix);
    }
}