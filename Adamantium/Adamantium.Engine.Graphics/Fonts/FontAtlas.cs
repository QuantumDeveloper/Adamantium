using System;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;
using System.Collections.Generic;
using Adamantium.Imaging;
using Adamantium.Imaging.PaletteQuantizer.Extensions;

namespace Adamantium.Engine.Graphics.Fonts
{
    public class FontAtlas : GraphicsResource
    {
        private TextureAtlasGenerator atlasGenerator;
        private Dictionary<uint, Glyph> processedGlyphs;
        
        private SamplerState assignedSamplerState;
        private BlendState assignedBlendState;
        private DepthStencilState assignedDepthStencilState;
        private RasterizerState assignedRasterizerState;
        private BlendState oldBlendState;
        private DepthStencilState oldDepthStencilState;
        private RasterizerState oldRasterizerState;
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

        public FontAtlas(GraphicsDevice device, Typeface typeface, FontParameters parameters) : base(device)
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
            
            atlasGenerator = new TextureAtlasGenerator(
                Typeface, 
                Font, 
                MSDFTextureSize, 
                SampleRate,
                PixelRange, 
                StartGlyphIndex,
                GlyphCount,
                SortingVariant,
                GlyphMargin);
            
            AtlasData = atlasGenerator.PrepareTextureAtlas();
            
            var description = new TextureDescription();
            description.Width = (uint)AtlasData.AtlasSize.Width;
            description.Height = (uint)AtlasData.AtlasSize.Height;
            description.Depth = 1;
            description.ArrayLayers = 1;
            description.MipLevels = 1;
            description.Samples = MSAALevel.None;
            description.Format = Format.R8G8B8A8_UNORM;
            description.InitialLayout = ImageLayout.Preinitialized;
            description.DesiredImageLayout = ImageLayout.ShaderReadOnlyOptimal;
            description.ImageType = ImageType._2d;
            description.ImageAspect = ImageAspectFlagBits.ColorBit;
            description.Usage = ImageUsageFlagBits.SampledBit | ImageUsageFlagBits.TransferDstBit | ImageUsageFlagBits.TransferSrcBit;
            description.Dimension = TextureDimension.Texture2D;
            
            Atlas = Texture.New(GraphicsDevice, description);
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

        private void ProcessGlyphs(params Glyph[] glyphs)
        {
            var uniqueGlyphs = glyphs.Distinct(x => x.Index);
            var glyphsToProcess = GetNotProcessedGlyphs(uniqueGlyphs);
            var textureDataArray = atlasGenerator.GenerateTextureForGlyphs(glyphsToProcess);

            if (textureDataArray.Length > 0)
            {
                ProcessTextureData(textureDataArray);
            }

            foreach (var glyph in glyphsToProcess)
            {
                processedGlyphs[glyph.Index] = glyph;
            }
        }

        private void ProcessTextureData(GlyphTextureData[] textureDataArray)
        {
            Atlas.TransitionImageLayout(ImageLayout.TransferDstOptimal);
            var commandBuffer = GraphicsDevice.BeginSingleTimeCommands();
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

                BufferImageCopy region = new BufferImageCopy();
                region.BufferOffset = 0;
                region.BufferRowLength = (uint)textureData.FullGlyphSize.Width;
                region.BufferImageHeight = (uint)textureData.FullGlyphSize.Height;
                region.ImageSubresource = new ImageSubresourceLayers();
                region.ImageSubresource.AspectMask = ImageAspectFlagBits.ColorBit;
                region.ImageSubresource.MipLevel = 0;
                region.ImageSubresource.BaseArrayLayer = 0;
                region.ImageSubresource.LayerCount = 1;
                region.ImageOffset = new Offset3D()
                    { X = textureData.BoundingRect.Left, Y = textureData.BoundingRect.Top, Z = 0 };
                region.ImageExtent = new Extent3D() { Width = (uint)textureData.FullGlyphSize.Width, Height = (uint)textureData.FullGlyphSize.Height, Depth = 1 };

                commandBuffer.CopyBufferToImage(buffer, Atlas, ImageLayout.TransferDstOptimal, 1, region);
            }

            GraphicsDevice.EndSingleTimeCommands(commandBuffer);
            Atlas.TransitionImageLayout(Atlas.Description.DesiredImageLayout);
            foreach (var buffer in buffers)
            {
                buffer?.Dispose();
            }
        }

        public RectangleF GetUVCoordinatesForGlyph(uint glyphIndex)
        {
            return AtlasData.GetUVCoordinatesForGlyph(glyphIndex);
        }

        public void Update(string text)
        {
            var glyphs = Font.TranslateIntoGlyphs(text);
            ProcessGlyphs(glyphs);
        }
    }
}