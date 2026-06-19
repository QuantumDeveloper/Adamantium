using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Imaging;
using Adamantium.Imaging.PaletteQuantizer.Extensions;
using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics.Fonts
{
    public class FontAtlas : GraphicsResource
    {
        private TextureAtlasGenerator atlasGenerator;
        private Dictionary<uint, Glyph> processedGlyphs;
        
        private SamplerState assignedSamplerState;
        private Color foregroundColor;
        private TextRenderingParameters renderingParameters;

        protected FontAtlasData AtlasData { get; }

        protected Typeface Typeface { get; }
        
        protected IFont Font { get; }

        internal Texture Atlas { get; set; }

        public uint MSDFTextureSize { get; }
        
        public byte SampleRate { get; }
        
        public float PixelRange { get; }
        
        public uint StartGlyphIndex { get; }
        
        public uint GlyphCount { get; }
        
        public GlyphSortingVariant SortingVariant { get; }
        
        public uint GlyphMargin { get; }

        public double LineSpacingMultiplier { get; set; }

        public FontAtlas(IGraphicsDevice device, Typeface typeface, FontParameters parameters, uint atlasSize = 1024) : base(device)
        {
            processedGlyphs = new Dictionary<uint, Glyph>();
            
            Typeface = typeface;
            Font = Typeface.GetFont(0);
            MSDFTextureSize = parameters.MsdfTextureSize;
            SampleRate = parameters.SampleRate;
            PixelRange = parameters.PixelRange;
            StartGlyphIndex = parameters.StartGlyphIndex;
            GlyphCount = parameters.GlyphCount == uint.MaxValue? typeface.GlyphCount : parameters.GlyphCount;
            SortingVariant = parameters.SortingVariant;
            GlyphMargin = parameters.GlyphMargin;
            LineSpacingMultiplier = Font.LineSpacingMultiplier;
            
            AtlasData = new FontAtlasData(MSDFTextureSize, new Size(atlasSize, atlasSize));
            
            atlasGenerator = new TextureAtlasGenerator(
                Typeface, 
                Font,
                AtlasData,
                parameters);
            
            var description = new TextureDescription
            {
                Width = atlasSize,
                Height = atlasSize,
                Depth = 1,
                ArrayLayers = 1,
                MipLevels = 1,
                Samples = MSAALevel.None,
                Format = Format.R8G8B8A8_UNORM,
                InitialLayout = ImageLayout.Preinitialized,
                DesiredImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageType = ImageType._2d,
                ImageAspect = ImageAspectFlagBits.ColorBit,
                Usage = ImageUsageFlagBits.SampledBit | ImageUsageFlagBits.TransferDstBit | ImageUsageFlagBits.TransferSrcBit,
                Dimension = TextureDimension.Texture2D
            };

            Atlas = Texture.New(GraphicsDevice, description, "Dynamic Font Atlas");
        }

        private Glyph[] GetNotProcessedGlyphs(IEnumerable<Glyph> glyphs)
        {
            var processed = new List<Glyph>();
            foreach (var glyph in glyphs)
            {
                if (!processedGlyphs.ContainsKey(glyph.Index))
                {
                    processed.Add(glyph);
                }
            }

            return processed.ToArray();
        }

        private void ProcessGlyphs(IReadOnlyList<Glyph> glyphs)
        {
            var uniqueGlyphs = glyphs.Distinct(x => x.Index);
            var glyphsToProcess = GetNotProcessedGlyphs(uniqueGlyphs);
            var textureDataArray = atlasGenerator.GenerateTextureForGlyphs(glyphsToProcess);

            if (textureDataArray.Count > 0)
            {
                ProcessTextureData(textureDataArray);
            }

            foreach (var glyph in glyphsToProcess)
            {
                processedGlyphs[glyph.Index] = glyph;
            }
        }

        private void ProcessTextureData(IReadOnlyList<GlyphTextureData> textureDataArray)
        {
            Atlas.TransitionImageLayout(ImageLayout.TransferDstOptimal);
            var commandBuffer = GraphicsDevice.BeginSingleTimeCommand();
            var buffers = new List<Buffer>();

            foreach (var textureData in textureDataArray)
            {
                if (textureData.IsEmpty) continue;

                var buffer = Buffer.New(
                    GraphicsDevice,
                    textureData.Pixels,
                    BufferUsageFlags.TransferSrc,
                    MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);
                buffers.Add(buffer);

                var region = new BufferImageCopy();
                region.BufferOffset = 0;
                region.BufferRowLength = (uint)textureData.FullGlyphSize.Width;
                region.BufferImageHeight = (uint)textureData.FullGlyphSize.Height;
                region.ImageSubresource = new ImageSubresourceLayers();
                region.ImageSubresource.AspectMask = ImageAspectFlagBits.ColorBit;
                region.ImageSubresource.MipLevel = 0;
                region.ImageSubresource.BaseArrayLayer = 0;
                region.ImageSubresource.LayerCount = 1;
                region.ImageOffset = new Offset3D()
                {
                    X = textureData.BoundingRect.Left,
                    Y = textureData.BoundingRect.Top,
                    Z = 0
                };
                region.ImageExtent = new Extent3D()
                {
                    Width = (uint)textureData.FullGlyphSize.Width, 
                    Height = (uint)textureData.FullGlyphSize.Height, 
                    Depth = textureData.DepthLayer
                };

                commandBuffer.CopyBufferToImage(buffer, Atlas, ImageLayout.TransferDstOptimal, 1, region);
            }

            GraphicsDevice.EndSingleTimeCommand(commandBuffer);
            Atlas.TransitionImageLayout(Atlas.Description.DesiredImageLayout);

            foreach (var textureData in textureDataArray)
            {
                textureData.Pixels = [];
            }
            
            foreach (var buffer in buffers)
            {
                buffer?.Dispose();
            }
        }

        public RectangleF GetUVCoordinatesForGlyph(uint glyphIndex)
        {
            return AtlasData.GetUVCoordinatesForGlyph(glyphIndex);
        }

        public GlyphTextureData GetGlyphData(uint glyphIndex)
        {
            return AtlasData.GetGlyphData(glyphIndex);
        }

        public void Update(string text)
        {
            var uniqueSymbols = new string(text.Distinct().ToArray());
            var glyphs = Font.TranslateIntoGlyphs(uniqueSymbols);
            ProcessGlyphs(glyphs);
        }
    }
}