using System.Runtime.InteropServices;

namespace Adamantium.Imaging;

public class FrameData
{
    public FrameData(byte[] rawPixels, ImageDescription description, uint mipLevel = 0)
    {
        RawPixels = rawPixels;
        Description = description;
        MipLevel = mipLevel;
    }
    
    public byte[] RawPixels { get; internal set; }

    public ImageDescription Description { get; }
    
    public uint MipLevel { get; set; }
    
    public long BufferSize => RawPixels.Length;

    public GCHandle GetHandle()
    {
        var handle = GCHandle.Alloc(RawPixels, GCHandleType.Pinned);
        return handle;
    }
}