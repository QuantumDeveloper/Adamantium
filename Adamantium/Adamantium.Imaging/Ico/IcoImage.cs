using System.Collections.Generic;

namespace Adamantium.Imaging.Ico;

public class IcoImage : IRawBitmap
{
    private List<MipLevelData> _mipLevels;
    public IcoImage(ImageDescription description)
    {
        _mipLevels = new List<MipLevelData>();
        Description = description;
    }

    public uint Width => Description.Width;
    public uint Height => Description.Height;
    public SurfaceFormat PixelFormat => Description.Format;

    public bool IsMultiFrame => false;
    
    public bool HasMipLevels => MipLevelsCount > 1;
    public uint MipLevelsCount => (uint)_mipLevels.Count;
    public uint NumberOfReplays => 0;
    public uint FramesCount => 0;
    
    public byte[] PixelBuffer { get; set; }

    public void AddMipLevel(MipLevelData mipData)
    {
        mipData.MipLevel = (uint)_mipLevels.Count;
        _mipLevels.Add(mipData);
    }
    
    public ImageDescription Description { get; set; }
    public byte[] GetRawPixels(uint frameIndex)
    {
        return _mipLevels[0].Pixels;
    }

    public MipLevelData GetMipLevelData(uint mipLevel)
    {
        var level = _mipLevels[(int)mipLevel];
        return level;
    }

    public ImageDescription GetImageDescription()
    {
        return Description;
    }

    public FrameData GetFrameData(uint frameIndex)
    {
        return new FrameData(PixelBuffer, Description);
    }
}