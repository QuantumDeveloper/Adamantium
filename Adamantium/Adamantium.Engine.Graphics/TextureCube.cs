using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics;

public class TextureCube : Texture
{
    internal TextureCube(GraphicsDevice device, ImageDescription description, byte[] rawPixels, ImageUsageFlagBits usage, ImageLayout desiredLayout) 
        : base(device, description, rawPixels, usage, desiredLayout) 
    {
        
    }

    public static TextureCube Load(GraphicsDevice device, 
        string[] files,
        ImageUsageFlagBits usage = ImageUsageFlagBits.SampledBit,
        ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal)
    {
        if (files.Length != 6)
            throw new ArgumentOutOfRangeException(nameof(files), "Texture cube should have exactly 6 files");
        
        var bitmaps = new List<IRawBitmap>();
        foreach (var file in files)
        {
            bitmaps.Add(BitmapLoader.Load(file));
        }

        var totalSize = bitmaps[0].TotalSizeInBytes;
        var rawPixels = new byte[totalSize * 6];
        var destinationHandle = GCHandle.Alloc(rawPixels, GCHandleType.Pinned);
        var dstPtr = destinationHandle.AddrOfPinnedObject();
        for (int i = 0; i < bitmaps.Count; i++)
        {
            var pixels = bitmaps[i].GetFrameData(0);
            var sourceHandle = pixels.GetHandle();
            
            Utilities.CopyMemory(IntPtr.Add(dstPtr, (int)((ulong)i * totalSize)), sourceHandle.AddrOfPinnedObject(), (long)totalSize);
            sourceHandle.Free();
        }
        
        destinationHandle.Free();

        var description = bitmaps[0].GetImageDescription();
        description.Dimension = TextureDimension.TextureCube;

        return new TextureCube(device, description, rawPixels, usage, desiredLayout);
    }
}