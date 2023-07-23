using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging.Gif
{
    public class GifImage : IRawBitmap
    {
        private readonly List<GifFrame> frames;
        private MipLevelData _defaultMipLevelData;
        
        public GifImage()
        {
            frames = new List<GifFrame>();
            GlobalColorTable = new List<ColorRGB>();
        }
        
        public List<ColorRGB> GlobalColorTable { get; internal set; }

        public IReadOnlyList<GifFrame> Frames => frames;

        public GifFrame CurrentFrame { get; internal set; }

        public byte ColorDepth { get; internal set; }

        public ScreenDescriptor Descriptor { get; set; }
        
        public ApplicationExtension ApplicationExtension { get; set; }

        public void AddFrame(GifFrame frame)
        {
            frames.Add(frame);
        }
        
        public byte[] DecodeFrame(uint frameIndex)
        {
            var frame = frameIndex >= frames.Count ? frames.LastOrDefault() : frames[(int)frameIndex];
            
            if (!frame.IsDecoded)
            {
                frame.IndexData = LZW.Decompress(frame.CompressedData.ToArray(), frame.LzwMinimumCodeSize);
                GetImageFromIndexStream(frame, frameIndex);
            }
            return frame.RawPixels;
        }

        private void GetImageFromIndexStream(GifFrame frame, uint frameIndex)
        {
            var width = frame.Descriptor.Width;
            var height = frame.Descriptor.Height;
            var colorTable = frame.ColorTable;

            byte[] pixels = null;
            int offset = 0;
            int bytesPerPixel = 4;

            if (frame.Interlaced)
            {
                frame.IndexData = GifDecoder.Deinterlace(frame);
            }

            if (frameIndex == 0 || 
                (frame.Descriptor.Width == Descriptor.Width && frame.Descriptor.Height == Descriptor.Height) || 
                (frame.GraphicControlExtension != null && frame.GraphicControlExtension.DisposalMethod == DisposalMethod.None))
            {
                pixels = new byte[width * height * bytesPerPixel];

                for (int i = 0; i < width * height; i++)
                {
                    var colors = colorTable[frame.IndexData[i]];
                    pixels[offset] = colors.R;
                    pixels[offset + 1] = colors.G;
                    pixels[offset + 2] = colors.B;
                    if (frame.GraphicControlExtension != null &&
                        frame.GraphicControlExtension.TransparentColorIndex == i)
                    {
                        pixels[offset + 3] = 0;
                    }
                    else
                    {
                        pixels[offset + 3] = 255;
                    }

                    offset += bytesPerPixel;
                }
            }
            else if (frameIndex > 0 && 
                     (frame.Descriptor.OffsetLeft != Descriptor.Width || frame.Descriptor.OffsetTop != Descriptor.Height))
                     //frame.GraphicControlExtension != null && 
                     //frame.GraphicControlExtension.DisposalMethod == DisposalMethod.DoNotDispose)
            {
                pixels = new byte[Descriptor.Width * Descriptor.Height * bytesPerPixel];
                var baseFrame = Frames[(int)frameIndex - 1];
                var originalIndexStream = baseFrame.IndexData;
                var currentIndexStream = new List<int>(originalIndexStream).ToArray();
                for (int i = 0; i < frame.Descriptor.Height; ++i)
                {
                    var dstIndex = ((frame.Descriptor.OffsetTop + i) * Descriptor.Width) + frame.Descriptor.OffsetLeft;
                    var srcIndex = i * frame.Descriptor.Width;
                    Array.Copy(frame.IndexData, srcIndex, currentIndexStream, dstIndex, frame.Descriptor.Width);
                }

                int transparentIndex = 0;
                bool transparencyAvailable = false;
                if (baseFrame.GraphicControlExtension != null && baseFrame.GraphicControlExtension.TransparencyAvailable)
                {
                    transparencyAvailable = baseFrame.GraphicControlExtension.TransparencyAvailable;
                    transparentIndex = baseFrame.GraphicControlExtension.TransparentColorIndex;
                }

                for (int i = 0; i < currentIndexStream.Length; i++)
                {
                    bool useBaseColorTable = false;
                    if (currentIndexStream[i] == transparentIndex && transparencyAvailable)
                    {
                        currentIndexStream[i] = originalIndexStream[i];
                        useBaseColorTable = true;
                    }

                    ColorRGB colors;
                    if (useBaseColorTable)
                    {
                        colors = baseFrame.ColorTable[currentIndexStream[i]];
                    }
                    else
                    {
                        colors = colorTable[currentIndexStream[i]];
                    }

                    pixels[offset] = colors.R;
                    pixels[offset + 1] = colors.G;
                    pixels[offset + 2] = colors.B;
                    if (frame.GraphicControlExtension != null &&
                        frame.GraphicControlExtension.TransparentColorIndex == i)
                    {
                        pixels[offset + 3] = 0;
                    }
                    else
                    {
                        pixels[offset + 3] = 255;
                    }
                    offset += bytesPerPixel;
                }
                
                frame.IndexData = currentIndexStream;
            }
            else
            {
                throw new NotImplementedException($"Current disposal method: {frame.GraphicControlExtension?.DisposalMethod} is not supported");
            }

            frame.RawPixels = pixels;
            frame.IsDecoded = true;
        }

        public uint Width => Descriptor.Width;

        public uint Height => Descriptor.Height;

        public SurfaceFormat PixelFormat => SurfaceFormat.R8G8B8A8.UNorm;
        public bool IsMultiFrame => frames.Count > 0;
        public bool HasMipLevels => false;
        public uint MipLevelsCount => 0;
        public uint NumberOfReplays => 0;
        public uint FramesCount => (uint)frames.Count;
        public byte[] GetRawPixels(uint frameIndex)
        {
            return DecodeFrame(frameIndex);
        }

        public MipLevelData GetMipLevelData(uint mipLevel)
        {
            return _defaultMipLevelData ??= new MipLevelData(GetImageDescription(), 0, GetRawPixels(0));
        }

        public ImageDescription GetImageDescription()
        {
            return new ImageDescription()
            {
                Width = Descriptor.Width,
                Height = Descriptor.Height,
                Depth = 1,
                Dimension = TextureDimension.Texture2D,
                Format = Format.R8G8B8A8_UNORM,
                ArraySize = 1,
                MipLevels = 1
            };
        }

        public FrameData GetFrameData(uint frameIndex)
        {
            var pixels = GetRawPixels(frameIndex);
            return new FrameData(pixels, GetImageDescription());
        }
    }
}
