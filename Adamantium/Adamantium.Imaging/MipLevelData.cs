namespace Adamantium.Imaging;

public class MipLevelData
{
    public MipLevelData(ImageDescription description)
    {
        Description = description;
    }
    
    public MipLevelData(ImageDescription description, uint level)
    {
        Description = description;
        MipLevel = level;
    }
    
    public MipLevelData(ImageDescription description, uint level, byte[] pixels)
    {
        Description = description;
        MipLevel = level;
        Pixels = pixels;
    }
    
    public uint MipLevel { get; set; }
    
    public ImageDescription Description { get; }
    
    public byte[] Pixels { get; set; }
}