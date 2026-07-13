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
        private bool _warnedLayersExhausted;
        
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

        // The dynamic atlas is a Texture2DArray: each layer is one atlasSize x atlasSize slice holding ~225 shelf-packed
        // glyphs, so N layers give ~N*225 glyph capacity (v1: fixed 8 => ~1800, enough for Latin + Cyrillic + symbols).
        // When the packer fills all layers it clamps to the last one (LayersExhausted) instead of the old past-256 crash.
        public const uint AtlasLayerCount = 8;

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
            
            AtlasData = new FontAtlasData(MSDFTextureSize, new Size(atlasSize, atlasSize), AtlasLayerCount);
            
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
                ArrayLayers = AtlasLayerCount,
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

            Prewarm();
        }

        // The characters a UI is overwhelmingly made of. Rasterized ONCE, in ONE bulk call, when the atlas is born.
        private const string PrewarmCharset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        // Rasterizing a glyph is MSDF work - measured at ~23 ms each (Debug). The atlas grows LAZILY, one Update(text) per text
        // block, so a cold fill introduced roughly one new character per block and paid that ~23 ms serially, ~50 times: 1.1 s
        // of a 1.9 s 4K-viewport fill, and by far its single biggest cost. The generator already parallelises across glyphs
        // (TextureAtlasGenerator.GenerateTextureForGlyphs) - it was just never given more than one at a time. Handing it the
        // whole base charset up front turns ~50 serial single-glyph rasterizations into ONE parallel batch, and every text
        // block that follows finds its glyphs already there. Anything outside this set still grows the atlas lazily, exactly
        // as before - the cost simply stops landing in the middle of a fill.
        private void Prewarm()
        {
            ProcessGlyphs(Font.TranslateIntoGlyphs(PrewarmCharset));
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

            if (AtlasData.LayersExhausted && !_warnedLayersExhausted)
            {
                _warnedLayersExhausted = true;
                System.Console.WriteLine($"[FONT] Dynamic atlas exhausted all {AtlasLayerCount} layers; further glyphs overwrite the last. Raise FontAtlas.AtlasLayerCount or add dynamic growth.");
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
                // The glyph goes into its ARRAY LAYER (DepthLayer), at its 2D position within that layer. Depth stays 1
                // (a 2D-array slice is 1 deep); the layer selects the slice via BaseArrayLayer. The old code put the layer
                // in ImageExtent.Depth on a 1-deep 2D image, which the GPU rejected once a second layer appeared.
                region.ImageSubresource.BaseArrayLayer = textureData.DepthLayer;
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
                    Depth = 1
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