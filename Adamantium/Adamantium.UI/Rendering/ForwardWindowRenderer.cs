using System;
using System.Diagnostics;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Diagnostics;
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
        Window.DpiChanged -= OnDpiChanged;
    }

    protected override void SubscribeToEvents()
    {
        Window.ClientSizeChanged += OnClientSizeChanged;
        Window.MSAALevelChanged += OnMSAALevelChanged;
        Window.DpiChanged += OnDpiChanged;
    }

    // On-screen: render at the window's device-pixel density. ClientSize is logical (DIP); the presenter/viewport are
    // sized ClientSize x RenderScale = physical px while the projection stays logical, so content scales crisply with
    // the monitor. Desktop DPI is uniform (X==Y) so the scalar RenderScale takes the X axis (per-axis is a refinement).
    public override void SetWindow(IWindow window)
    {
        if (window != null) RenderScale = window.DpiScale.X;
        base.SetWindow(window);
    }

    private void OnDpiChanged(object sender, EventArgs e)
    {
        RenderScale = Window.DpiScale.X;
        UpdateWindowResources();   // resize the presenter/viewport to the new physical size + re-init
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
        var t0 = Stopwatch.GetTimestamp();
        _renderCache.Render(GraphicsDevice, Scissor);
        RuntimeStats.LastRenderDrawMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    public override void PreRender()
    {
        if (Window == null) return;
        _renderCache.PreRender();
    }

    public override void PrepareData()
    {
        if (Window == null) return;

        var t0 = Stopwatch.GetTimestamp();
        _renderCache.BuildFromVisualTree(Window);
        var buildMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        // Skip the per-unit transform re-bake (proc) when nothing MOVED: a Clean frame (nothing changed at all) or a
        // GEOMETRY-ONLY partial (a hover re-recorded some draw contents, but no transform changed). Proc walks EVERY unit
        // (O(N)) and the draw pass re-bakes each drawn unit anyway, so on a big list a hover would otherwise pay an O(N)
        // re-bake for nothing - the mouse-move FPS drop. Only a real move (transform-dirty partial, or a full re-layout)
        // needs it before PreRender reads the baked transforms.
        if (_renderCache.LastBuildKind == RenderBuildKind.Clean
            || (_renderCache.LastBuildKind == RenderBuildKind.Partial && !_renderCache.LastBuildTransformDirty))
        {
            RuntimeStats.LastRenderBuildMs = buildMs;
            RuntimeStats.LastRenderProcMs = 0;
            return;
        }
        var t1 = Stopwatch.GetTimestamp();
        _renderCache.ProcessCommands(Window.GetProjectionMatrix(), RenderScale);
        RuntimeStats.LastRenderBuildMs = buildMs;
        RuntimeStats.LastRenderProcMs = Stopwatch.GetElapsedTime(t1).TotalMilliseconds;
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