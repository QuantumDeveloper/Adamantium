using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.Input;

/// <summary>Turns a readback snapshot bitmap into the premultiplied BGRA buffer the drag ghost hands the OS compositor.</summary>
public static class DragGhostPixels
{
    /// <summary>Premultiply straight-alpha BGRA (the readback layout) in place into a new buffer: each colour channel
    /// scaled by alpha/255, as <c>UpdateLayeredWindow</c> (and a premultiplied CGImage) require.</summary>
    public static byte[] ToPremultipliedBgra(BitmapSource source)
    {
        var src = source?.PixelBytes;
        if (src == null) return null;

        var dst = new byte[src.Length];
        for (int i = 0; i + 3 < src.Length; i += 4)
        {
            byte a = src[i + 3];
            dst[i] = (byte)(src[i] * a / 255);
            dst[i + 1] = (byte)(src[i + 1] * a / 255);
            dst[i + 2] = (byte)(src[i + 2] * a / 255);
            dst[i + 3] = a;
        }
        return dst;
    }
}
