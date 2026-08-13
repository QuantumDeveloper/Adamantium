using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Vector icons tab: one DrawingImage resource shown at many sizes at once. The point of the stand is that the
/// biggest and the smallest come from the SAME drawing and the same mesh - so the edges must stay clean at every size,
/// and dragging a colour or an angle must move all of them together.</summary>
[ViewModel]
public partial class VectorIconsViewModel : TabPageViewModel
{
    public VectorIconsViewModel() : base("Vector icons") { }

    [Bindable] private double _showcaseSize = 220;

    [Bindable] private double _rotation;

    [Bindable] private Color _accent = Colors.DeepSkyBlue;

    [Bindable] private Color _body = new(226, 232, 240, 255);

    [Bindable] private string _caption = "SVG-free";

    [Bindable] private Color _captionColor = new(11, 18, 32, 255);
}
