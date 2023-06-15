using System;
using Adamantium.Imaging;

namespace Adamantium.UI.RoutedEvents;

public class RenderTargetEventArgs : RoutedEventArgs
{
    public IntPtr Handle { get; }
    public UInt32 Width { get; }
    public UInt32 Height { get; }
    public SurfaceFormat PixelFormat { get; }

    public RenderTargetEventArgs(IntPtr pointer, UInt32 width, UInt32 height, SurfaceFormat format)
    {
        Handle = pointer;
        Width = width;
        Height = height;
        PixelFormat = format;
    }
}