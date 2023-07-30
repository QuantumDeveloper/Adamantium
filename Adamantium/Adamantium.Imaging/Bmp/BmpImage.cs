namespace Adamantium.Imaging.Bmp;

public class BmpImage : IRawBitmap
{
    public BmpImage(uint width, uint height, SurfaceFormat pixelFormat)
    {
        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
    }

    public BmpImage(ImageDescription description)
    {
        Description = description;
        Width = description.Width;
        Height = description.Height;
        PixelFormat = Description.Format;
    }
    
    public byte[] PixelData { get; set; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public SurfaceFormat PixelFormat { get; private set; }

    public bool IsMultiFrame => false;
    public bool HasMipLevels => false;
    public uint MipLevelsCount => 0;
    public uint NumberOfReplays => 0;
    public uint FramesCount => 1;
    
    public ImageDescription Description { get; }
    
    public byte[] GetRawPixels(uint frameIndex)
    {
        return PixelData;
    }

    public FrameData GetMipLevelData(uint mipLevel)
    {
        return GetFrameData(0);
    }

    public ImageDescription GetImageDescription()
    {
        return Description;
    }

    public FrameData GetFrameData(uint frameIndex)
    {
        return new FrameData(PixelData, Description);
    }
}