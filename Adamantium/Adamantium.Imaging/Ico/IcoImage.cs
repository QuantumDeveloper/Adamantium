using System.Collections.Generic;

namespace Adamantium.Imaging.Ico;

public class IcoImage : IRawBitmap
{
    private List<IcoMipLevelData> _mipLevels;
    public IcoImage(ImageDescription description)
    {
        _mipLevels = new List<IcoMipLevelData>();
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

    public void AddMipLevel(IcoMipLevelData mipData)
    {
        _mipLevels.Add(mipData);
    }
    
    public ImageDescription Description { get; set; }
    public byte[] GetFrameData(uint frameIndex)
    {
        return _mipLevels[0].Pixels;
    }

    public byte[] GetMipLevelData(uint mipLevel, out ImageDescription description)
    {
        var level = _mipLevels[(int)mipLevel];
        description = level.Description;
        return level.Pixels;
    }

    public ImageDescription GetImageDescription()
    {
        return Description;
    }
}

public class IcoMipLevelData
{
    public IcoMipLevelData(ImageDescription description)
    {
        Description = description;
    }
    
    public ImageDescription Description { get; }
    
    public byte[] Pixels { get; set; }
}