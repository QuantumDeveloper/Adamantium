namespace Adamantium.Imaging;

public class FrameData
{
    public FrameData(byte[] rawPixels, ImageDescription description)
    {
        RawPixels = rawPixels;
        Description = description;
    }
    
    public byte[] RawPixels { get; internal set; }

    public ImageDescription Description { get; }
    
    public long BufferSize => RawPixels.Length;
}