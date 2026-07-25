using Adamantium.MVVM;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.Rendering;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Visual → Image tab: the engine's UWP-RenderTargetBitmap analog. Type AUML, hit Render, and
/// <see cref="IVisualRenderer"/> parses it into a fresh tree, draws it OFF-SCREEN into a texture, reads it back and hands
/// back a bitmap the Image shows. It is the foundation for the drag-drop ghost and VisualBrush/DrawingBrush.</summary>
[ViewModel]
public partial class VisualRenderDemoViewModel : TabPageViewModel
{
    private readonly IVisualRenderer _renderer;

    public VisualRenderDemoViewModel(IVisualRenderer renderer) : base("Visual → Image")
    {
        _renderer = renderer;
    }

    // A self-contained snippet (explicit colours, no theme resources / fonts) so it renders standalone off-screen. No fixed
    // Height: the panel sizes to its content and the render auto-fits it, so nothing is clipped.
    [Bindable] private string _aumlText =
        "<StackPanel xmlns='http://adamantium/ui' Orientation='Vertical' Width='220' Background='#FF1B2430'>\n" +
        "  <Border Background='#FFE23B3B' CornerRadius='10' Width='170' Height='52' Margin='16'/>\n" +
        "  <Ellipse Fill='#FF3BE27A' Width='80' Height='48' Margin='16,8,16,16'/>\n" +
        "</StackPanel>";

    // The rendered bitmap (image DATA, not a control) - shown by an Image. Rendered on demand so the off-screen device is
    // resolved only after the app is fully up.
    [Bindable] private ImageSource _snapshot;

    [Command]
    private void RenderSnapshot()
    {
        // No size -> the renderer measures the tree and captures it at its OWN size (never clipped).
        Snapshot = _renderer.Render(AumlText);
    }

    // Snapshot of the LIVE, on-screen tree (image DATA). Captured by rendering the live element's subtree through a
    // parallel render cache (never disturbing the window). Toggle the live controls, then Snapshot, and the bitmap
    // reflects their CURRENT state.
    [Bindable] private ImageSource _liveSnapshot;

    // The element to snapshot arrives as a CommandParameter ({Binding ElementName=...}) - a transient render target, not
    // view-model state; the VM never holds a UI element. The request is QUEUED and rendered on the loop thread (serialised
    // with the window's render, never racing it); the bitmap comes back on the UI thread via the callback.
    [Command]
    private void SnapshotLive(object element)
    {
        if (element is IUIComponent live)
        {
            _renderer.RequestSnapshot(live, img => LiveSnapshot = img);
        }
    }
}
