namespace Adamantium.Imaging.Dds;

public class DdsImage : IRawBitmap
{
    public DdsImage(ImageDescription description)
    {
        Description = description;
    }
    
    public MipLevelData[] MipLevels { get; set; }
    public FrameData[] PixelBuffers { get; set; }
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
    
    public byte[] GetRawPixels(uint frameIndex)
    {
        if (Description.Dimension == TextureDimension.TextureCube)
        {
            return PixelBuffers[frameIndex].RawPixels;
        }
        return PixelBuffers[0].RawPixels;
    }

    public MipLevelData GetMipLevelData(uint mipLevel)
    {
        var mipData = MipLevels[mipLevel];
        return mipData;
    }


    public ImageDescription GetImageDescription()
    {
        return Description;
    }

    public FrameData GetFrameData(uint frameIndex)
    {
        return new FrameData(GetRawPixels(frameIndex), Description);
    }
}