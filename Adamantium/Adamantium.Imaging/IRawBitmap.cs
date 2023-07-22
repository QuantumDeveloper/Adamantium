namespace Adamantium.Imaging;

public interface IRawBitmap
{
    public uint Width { get; }
    
    public uint Height { get; }
    
    public SurfaceFormat PixelFormat { get; }

    public ulong TotalSizeInBytes => Width * Height * (ulong)PixelFormat.SizeInBytes;
    
    public bool IsMultiFrame { get; } 
    
    public bool HasMipLevels { get; }
    
    public uint MipLevelsCount { get; }
    
    public uint NumberOfReplays { get; }
        
    public uint FramesCount { get; }
        
    public byte[] GetFrameData(uint frameIndex);

    public byte[] GetMipLevelData(uint mipLevel, out ImageDescription description);

    public ImageDescription GetImageDescription();
}