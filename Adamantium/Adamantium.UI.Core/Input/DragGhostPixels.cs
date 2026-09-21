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

    // Bilinear-resample a premultiplied-BGRA bitmap to a new size (premultiplied alpha resamples cleanly - no dark fringe).
    // Used to re-scale the drag ghost when it crosses to a monitor of a different DPI so it keeps its physical size.
    public static byte[] Resample(byte[] src, int sw, int sh, int dw, int dh)
    {
        if (src == null || dw <= 0 || dh <= 0 || sw <= 0 || sh <= 0) return src;
        var dst = new byte[dw * dh * 4];
        double fx = (double)sw / dw, fy = (double)sh / dh;
        for (int y = 0; y < dh; y++)
        {
            double syf = (y + 0.5) * fy - 0.5;
            int sy = (int)Math.Floor(syf); double wy = syf - sy;
            int sy0 = Math.Clamp(sy, 0, sh - 1), sy1 = Math.Clamp(sy + 1, 0, sh - 1);
            for (int x = 0; x < dw; x++)
            {
                double sxf = (x + 0.5) * fx - 0.5;
                int sx = (int)Math.Floor(sxf); double wx = sxf - sx;
                int sx0 = Math.Clamp(sx, 0, sw - 1), sx1 = Math.Clamp(sx + 1, 0, sw - 1);
                int di = (y * dw + x) * 4;
                int i00 = (sy0 * sw + sx0) * 4, i10 = (sy0 * sw + sx1) * 4, i01 = (sy1 * sw + sx0) * 4, i11 = (sy1 * sw + sx1) * 4;
                for (int ch = 0; ch < 4; ch++)
                {
                    double top = src[i00 + ch] + (src[i10 + ch] - src[i00 + ch]) * wx;
                    double bot = src[i01 + ch] + (src[i11 + ch] - src[i01 + ch]) * wx;
                    dst[di + ch] = (byte)Math.Clamp(top + (bot - top) * wy, 0, 255);
                }
            }
        }
        return dst;
    }

    // Composite a pre-rendered count-badge bitmap onto the top-right CORNER of the ghost, expanding the canvas so the badge
    // sits at the corner (overflowing up/right by half its size) instead of covering the content. Both buffers are
    // premultiplied BGRA; the badge is alpha-composited "over" the body. Returns a new, larger buffer.
    public static (byte[] bgra, int w, int h) WithCornerBadge((byte[] bgra, int w, int h) body, (byte[] bgra, int w, int h) badge)
    {
        if (body.bgra == null) return badge;
        if (badge.bgra == null) return body;

        int ovR = badge.w / 2, ovT = badge.h / 2;               // the badge's centre sits on the body's top-right corner
        int w = body.w + (badge.w - ovR);                       // exact right extent (handles an odd badge width - no clip)
        int h = Math.Max(ovT + body.h, badge.h);
        var dst = new byte[w * h * 4];

        Blit(dst, w, h, body.bgra, body.w, body.h, 0, ovT);                      // body below the top overflow band
        Blit(dst, w, h, badge.bgra, badge.w, badge.h, body.w - ovR, 0);          // badge centred on the body's top-right corner
        return (dst, w, h);
    }

    // Alpha-composite a premultiplied-BGRA source OVER a premultiplied-BGRA destination at (ox, oy): dst = src + dst*(1-srcA).
    private static void Blit(byte[] dst, int dstW, int dstH, byte[] src, int sw, int sh, int ox, int oy)
    {
        for (int y = 0; y < sh; y++)
        {
            int dy = oy + y;
            if (dy < 0 || dy >= dstH) continue;
            for (int x = 0; x < sw; x++)
            {
                int dx = ox + x;
                if (dx < 0 || dx >= dstW) continue;
                int si = (y * sw + x) * 4;
                byte sa = src[si + 3];
                if (sa == 0) continue;
                int di = (dy * dstW + dx) * 4;
                int ia = 255 - sa;
                dst[di] = (byte)(src[si] + dst[di] * ia / 255);
                dst[di + 1] = (byte)(src[si + 1] + dst[di + 1] * ia / 255);
                dst[di + 2] = (byte)(src[si + 2] + dst[di + 2] * ia / 255);
                dst[di + 3] = (byte)(sa + dst[di + 3] * ia / 255);
            }
        }
    }
}
