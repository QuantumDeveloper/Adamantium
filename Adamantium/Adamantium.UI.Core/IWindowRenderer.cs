using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Core;

public interface IWindowRenderer : IDisposable
{
    public IDrawingContext DrawingContext { get; }
    public bool IsRendererUpToDate { get; }
    public bool FirstFrameProcessed { get; }

    /// <summary>Render resolution multiplier over the window's logical size: presenter + viewport are sized
    /// ClientSize x RenderScale, while the projection stays the logical size. 1 = on-screen 1:1; the designer sets it
    /// to the zoom factor so geometry/text re-rasterise crisply at design x scale.</summary>
    public double RenderScale { get; set; }
        
    public void SetWindow(IWindow window);

    /// <summary>Re-points the renderer at a different window, reusing (resizing) the existing presenter instead of
    /// recreating it. Used by the designer to switch the previewed window without render-target churn.</summary>
    public void Retarget(IWindow window);

    public void Render(AppTime appTime);

    /// <summary>Records out-of-render-pass work (shared-surface latch copies) before BeginRendering.</summary>
    public void PreRender();

    public void PrepareData();

    /// <summary>DEVICE-FREE record half of PrepareData (Phase 3.2): walk the tree + component.Render into the packet. Safe
    /// to run at loop level after the whole Update phase, off the GPU/BeginDraw fence.</summary>
    public void RecordData();

    /// <summary>GPU apply half of PrepareData (Phase 3.2): realize the recorded packet into units + the per-unit transform
    /// re-bake. Runs inside BeginDraw (after the fence); moves to the render thread in Phase 3.3.</summary>
    public void ApplyData();

    /// <summary>Disposes the cached render units (for a fresh tree each frame, e.g. the headless designer). No-op for
    /// renderers that keep their cache across frames via attachment-based reconciliation (the on-screen path).</summary>
    public void ResetCache();

    public GraphicsPresenter Presenter { get; }

    public void OnFrameEnded();

    public void Present();

    public void ResizePresenter(PresentationParameters parameters);
    public void ResizePresenter(uint width, uint height);
}