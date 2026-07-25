using System;
using System.Collections.Generic;
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

    /// <summary>Composite several premultiplied-BGRA bitmaps into ONE, stacked top-to-bottom (left-aligned) with a
    /// transparent <paramref name="gap"/> between them - the ghost of a multi-item drag. Width = the widest part.</summary>
    public static (byte[] bgra, int width, int height) StackVertical(IReadOnlyList<(byte[] bgra, int w, int h)> parts, int gap)
    {
        int width = 0, height = 0;
        foreach (var p in parts)
        {
            if (p.bgra == null) continue;
            width = Math.Max(width, p.w);
            height += p.h;
        }
        int count = 0;
        foreach (var p in parts) if (p.bgra != null) count++;
        height += gap * Math.Max(0, count - 1);
        if (width == 0 || height == 0) return (null, 0, 0);

        var dst = new byte[width * height * 4];
        int y = 0;
        foreach (var p in parts)
        {
            if (p.bgra == null) continue;
            int rowBytes = p.w * 4;
            for (int row = 0; row < p.h; row++)
            {
                Array.Copy(p.bgra, row * rowBytes, dst, ((y + row) * width) * 4, rowBytes);
            }
            y += p.h + gap;
        }
        return (dst, width, height);
    }
}
