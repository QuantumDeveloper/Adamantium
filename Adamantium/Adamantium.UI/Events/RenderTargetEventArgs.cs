using System;
using Adamantium.Imaging;

namespace Adamantium.UI.Events;

public class RenderTargetEventArgs : EventArgs
{
    public IntPtr Handle { get; }
    public Int32 Width { get; }
    public Int32 Height { get; }
    public SurfaceFormat PixelFormat { get; }

    public RenderTargetEventArgs(IntPtr pointer, Int32 width, Int32 height, SurfaceFormat format)
    {
        Handle = pointer;
        Width = width;
        Height = height;
        PixelFormat = format;
    }
}