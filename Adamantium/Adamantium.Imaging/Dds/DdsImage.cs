using System.Linq;

namespace Adamantium.Imaging.Dds;

public class DdsImage : IRawBitmap
{
    public DdsImage(ImageDescription description)
    {
        Description = description;
    }
    
    public PixelBuffer[] PixelBuffers { get; set; }
    public uint Width => Description.Width;
    public uint Height => Description.Height;
    public SurfaceFormat PixelFormat => Description.Format;
    
    public bool IsMultiFrame => false;
    public bool HasMipLevels => Description.MipLevels > 1;
    public uint MipLevelsCount => Description.MipLevels;
    public uint NumberOfReplays => 0;

    public uint FramesCount
    {
        get
        {
            if (Description.Dimension == TextureDimension.TextureCube)
            {
                return (uint)PixelBuffers.Length;
            }

            return 1;
        }
    }
    
    public ImageDescription Description { get; }
    
    public byte[] GetFrameData(uint frameIndex)
    {
        if (Description.Dimension == TextureDimension.TextureCube)
        {
            return PixelBuffers[frameIndex].GetPixels<byte>();
        }
        return PixelBuffers[0].GetPixels<byte>();
    }

    public byte[] GetMipLevelData(uint mipLevel, out ImageDescription description)
    {
        var buffer = PixelBuffers.FirstOrDefault(x => x.MipLevel == mipLevel);
        var descr = buffer.MipMapDescription;
        description = new ImageDescription()
        {
            Width = descr.Width,
            Height = descr.Height,
            Depth = 1,
            Dimension = TextureDimension.Texture2D,
            Format = Description.Format,
            ArraySize = 1
        };
        return buffer?.GetPixels<byte>();
    }

    public ImageDescription GetImageDescription()
    {
        return Description;
    }
}