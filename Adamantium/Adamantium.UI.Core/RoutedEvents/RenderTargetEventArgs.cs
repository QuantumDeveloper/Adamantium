using Adamantium.Imaging;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.RoutedEvents;

public class RenderTargetEventArgs : RoutedEventArgs
{
    public RenderTargetImage RenderTarget { get; }
    public UInt32 Width { get; }
    public UInt32 Height { get; }
    public SurfaceFormat PixelFormat { get; }

    public RenderTargetEventArgs(RenderTargetImage renderTarget, UInt32 width, UInt32 height, SurfaceFormat format)
    {
        RenderTarget = renderTarget;
        Width = width;
        Height = height;
        PixelFormat = format;
    }
}