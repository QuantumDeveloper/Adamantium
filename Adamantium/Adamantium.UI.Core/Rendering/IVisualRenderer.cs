using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.Rendering;

/// <summary>
/// Renders a visual into a texture off-screen - the engine's analog of UWP's <c>RenderTargetBitmap</c>. The result is a
/// drawable <see cref="ImageSource"/> (feed it to an <c>Image.Source</c> / <c>DrawImage</c>). Behind an interface so it is
/// mockable in tests and resolvable via DI. Shared foundation for the drag-drop ghost, VisualBrush/DrawingBrush bakes, and
/// previews/thumbnails (see docs/DRAG_DROP_PLAN.md Phase 0).
/// </summary>
public interface IVisualRenderer
{
    /// <summary>
    /// Renders <paramref name="visual"/> at <paramref name="size"/> (logical) x <paramref name="scale"/> (device px) into a
    /// fresh off-screen target. Returns the drawable image, or null if the frame could not begin. The returned image owns
    /// its GPU resources - dispose it when done. Pass a fresh / detached visual (hosting reparents it); a live on-screen
    /// element is baked separately without reparenting.
    /// </summary>
    ImageSource Render(IUIComponent visual, Size size, double scale = 1.0, Color? clearColor = null);

    /// <summary>
    /// Parses <paramref name="aumlText"/> into a fresh visual tree and renders it (UWP's <c>XamlReader.Load</c> +
    /// <c>RenderTargetBitmap</c> in one). The tree is brand-new / detached, so this is always the clean, no-reparent case.
    /// Returns null if the markup does not produce a visual. Self-contained markup renders best - a snippet leaning on theme
    /// resources may not resolve them without a live context.
    /// </summary>
    ImageSource Render(string aumlText, Size size, double scale = 1.0, Color? clearColor = null);

    /// <summary>Renders the visual at its OWN desired size (measured unconstrained) - the RenderTargetBitmap default, so the
    /// whole element is captured and never clipped by a fixed size.</summary>
    ImageSource Render(IUIComponent visual, double scale = 1.0, Color? clearColor = null);

    /// <summary>Parses AUML and renders it at the tree's own desired size (auto-fit, never clipped).</summary>
    ImageSource Render(string aumlText, double scale = 1.0, Color? clearColor = null);
}
