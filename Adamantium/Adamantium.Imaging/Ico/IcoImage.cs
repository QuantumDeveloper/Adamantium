using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Imaging.Ico;

public class IcoImage : IRawBitmap
{
    private List<FrameData> _pixelBuffers;
    public IcoImage(ImageDescription description)
    {
        _pixelBuffers = new List<FrameData>();
        Description = description;
    }

    public uint Width => Description.Width;
    public uint Height => Description.Height;
    public SurfaceFormat PixelFormat => Description.Format;

    public bool IsMultiFrame => false;
    
    public bool HasMipLevels => MipLevelsCount > 1;
    public uint MipLevelsCount => (uint)PixelBuffers.Count;
    public uint NumberOfReplays => 0;
    public uint FramesCount => 0;

    public IReadOnlyList<FrameData> PixelBuffers => _pixelBuffers; 

    public void AddMipLevel(FrameData mipData)
    {
        _pixelBuffers.Add(mipData);
    }
    
    public ImageDescription Description { get; set; }
    public byte[] GetRawPixels(uint frameIndex)
    {
        return PixelBuffers[0].RawPixels;
    }

    public FrameData GetMipLevelData(uint mipLevel)
    {
        return PixelBuffers.FirstOrDefault(x => x.MipLevel == mipLevel);
    }

    public ImageDescription GetImageDescription()
    {
        return Description;
    }

    public FrameData GetFrameData(uint frameIndex)
    {
        return PixelBuffers[(int)frameIndex];
    }
}