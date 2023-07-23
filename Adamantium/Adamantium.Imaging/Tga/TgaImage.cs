namespace Adamantium.Imaging.Tga;

public class TgaImage : IRawBitmap
{
    public TgaImage(uint width, uint height, SurfaceFormat format)
    {
        Width = width;
        Height = height;
        PixelFormat = format;
    }
    
    public uint Width { get; }
    public uint Height { get; }
    public SurfaceFormat PixelFormat { get; }

    public bool IsMultiFrame => false;

    public bool HasMipLevels => false;
    public uint MipLevelsCount => 0;
    public uint NumberOfReplays => 0;
    public uint FramesCount => 1;
    
    public byte[] PixelBuffer { get; set; }
    
    public byte[] GetRawPixels(uint frameIndex)
    {
        return PixelBuffer;
    }

    public MipLevelData GetMipLevelData(uint mipLevel)
    {
        return new MipLevelData(GetImageDescription(), 0, PixelBuffer);
    }

    public ImageDescription GetImageDescription()
    {
        ImageDescription description = new ImageDescription();
        description.Width = Width;
        description.Height = Height;
        description.Depth = 1;
        description.Dimension = TextureDimension.Texture2D;
        description.ArraySize = 1;
        description.MipLevels = 1;
        description.Format = PixelFormat;
        return description;
    }

    public FrameData GetFrameData(uint frameIndex)
    {
        return new FrameData(PixelBuffer, GetImageDescription());
    }
}