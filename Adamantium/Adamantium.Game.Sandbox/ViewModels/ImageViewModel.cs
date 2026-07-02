using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Image tab: shows the image-format/animation variations (static PNG, APNG, GIF, TGA, JPG, mip-mapped) inside
/// a ScrollViewer. One <see cref="Opacity"/> slider fades them all through the view-model.</summary>
[ViewModel]
public partial class ImageViewModel : TabPageViewModel
{
    public ImageViewModel() : base("Image") { }

    [Bindable] private double _opacity = 1;
}
