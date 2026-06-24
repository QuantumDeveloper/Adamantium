namespace Adamantium.UI.Rendering.RenderUnits;

// Live, app-global switch for the GPU analytic AA (fill coverage fringe + feathered strokes). Refreshed per
// window-render from IWindow.AnalyticAntialiasing in WindowRenderService.BeginDraw - windows render serially, so the
// flag reflects the window currently drawing. The fill/stroke components read it every frame (in Render/PreRender /
// ComputeFringe), so the window property toggles it WITHOUT rebuilding the render cache - ideal for A/B comparison.
internal static class AnalyticAa
{
    public static bool Enabled = true;
}
