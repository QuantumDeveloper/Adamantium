using System.Collections.Generic;
using Adamantium.Imaging.Jpeg.Decoder;

namespace Adamantium.Imaging.Jpeg;

public class JpegImage : IRawBitmap
{
    private List<JpegFrame> _frames;
    private FrameData _defaultFrame;

    public JpegImage()
    {
        _frames = new List<JpegFrame>();
    }

    public uint Width { get; set; }
    public uint Height { get; set; }
    public SurfaceFormat PixelFormat { get; set; }
    
    public bool IsMultiFrame => false;
    public bool HasMipLevels => false;
    public uint MipLevelsCount => 0;
    public uint NumberOfReplays => 0;
    public uint FramesCount => (uint)_frames.Count;
    public byte[] GetRawPixels(uint frameIndex)
    {
        if (FramesCount == 1)
        {
            return _frames[0].PixelData;
        }

        return _frames[(int)frameIndex].PixelData;
    }

    internal void AddFrame(JpegFrame frame)
    {
        _frames.Add(frame);
    }

    public MipLevelData GetMipLevelData(uint mipLevel)
    {
        return new MipLevelData(GetImageDescription(), 0) { Pixels = GetRawPixels(0) };
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
        if (FramesCount > 1)
        {
            return new FrameData(GetRawPixels(frameIndex), GetImageDescription());
        }

        return _defaultFrame ??= new FrameData(GetRawPixels(0), GetImageDescription());
    }
}