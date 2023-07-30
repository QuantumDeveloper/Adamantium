using System;
using Adamantium.Imaging;

namespace Adamantium.UI.Media.Imaging;

public class BitmapFrame : BitmapSource
{
    public BitmapFrame(
        UInt32 width,
        UInt32 height,
        double dpiX,
        double dpiY,
        SurfaceFormat format,
        byte[] pixels,
        uint index)
        :base(
            width,
            height,
            dpiX,
            dpiY,
            format,
            pixels
        )
    {
        Index = index;
    }
    
    public uint Index { get; }
}