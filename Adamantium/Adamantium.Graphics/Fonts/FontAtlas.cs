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

        // Glyphs whose MSDF is being generated on a worker RIGHT NOW, and the finished data waiting to be uploaded.
        // Generation is arithmetic and needs no device; the upload does - so the two live on different threads and meet
        // here.
        private readonly HashSet<uint> _inFlight = new();
        private readonly Queue<IReadOnlyList<GlyphTextureData>> _ready = new();
        private readonly Dictionary<uint, Glyph> _generated = new();
        private readonly object _asyncGate = new();

        /// <summary>Bumped whenever glyphs LAND in the atlas. A text block built while some of its glyphs were still
        /// being rasterized compares this against the version it built at and rebuilds when they differ - that is what
        /// makes an asynchronous fill appear without anybody polling for a particular glyph.</summary>
        public int Version { get; private set; }

        /// <summary>True while any glyph for this atlas is still being rasterized or waiting to be uploaded.</summary>
        public bool HasPendingGlyphs
        {
            get
            {
                lock (_asyncGate)
                {
                    return _inFlight.Count > 0 || _ready.Count > 0;
                }
            }
        }

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
        }

        /// <summary>
        /// Rasterize every character of <paramref name="text"/> this atlas does not have yet - in ONE batch.
        ///
        /// The atlas grows LAZILY, one <see cref="Update"/> per text block, and that is deliberate: a UI must not pay for
        /// glyphs it never shows. But a lazy path hands the generator only the characters ONE block introduced, and
        /// rasterizing a glyph is MSDF work - ~23 ms in Debug. A cold fill realizes ~50 new text blocks, each contributing
        /// about one new character, so ~50 glyphs were rasterized one after another, on a single core: 1.1 s of a 1.9 s 4K
        /// viewport fill, and by far its biggest single cost.
        ///
        /// The generator ALREADY parallelises across glyphs (TextureAtlasGenerator.GenerateTextureForGlyphs) - it was simply
        /// never given more than one at a time. So the fix is not to abandon laziness (prewarming a charset up front just moves
        /// the same cost into startup, and pays for glyphs nobody asked for) - it is to let a caller that is about to build
        /// MANY blocks pool their characters and warm them together. Nothing is rasterized that the UI does not use; the work
        /// simply stops being serial.
        /// </summary>
        public void Warm(string text)
        {
            if (!string.IsNullOrEmpty(text)) Update(text);
        }

        /// <summary>Ask for a text's glyphs WITHOUT waiting for them. What is missing goes to a worker (MSDF generation is
        /// arithmetic - no device, no shared mutable state), and the result is uploaded later by <see cref="PumpReady"/> on
        /// the thread that owns the device. The frame does not stop for it: text draws with the glyphs it has and the rest
        /// arrive over the next frames, each landing bumping <see cref="Version"/> so the blocks rebuild themselves.
        /// <para>Measured on the Brushes tab: 80 new glyphs cost 650-830 ms of MSDF, which was 88% of the apply phase and
        /// the single biggest item in opening a tab. It is the same work either way - it simply stops being in the way.</para></summary>
        public void RequestAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // A render with no "next frame" (a bitmap, a preview, an off-screen test) cannot let its letters arrive later.
            if (FontAtlasStore.SynchronousFill)
            {
                Update(text);
                Version++;
                return;
            }

            var uniqueSymbols = new string(text.Distinct().ToArray());
            var glyphs = Font.TranslateIntoGlyphs(uniqueSymbols);

            List<Glyph> toGenerate = null;
            lock (_asyncGate)
            {
                foreach (var glyph in glyphs.Distinct(x => x.Index))
                {
                    if (processedGlyphs.ContainsKey(glyph.Index) || !_inFlight.Add(glyph.Index)) continue;
                    (toGenerate ??= new List<Glyph>()).Add(glyph);
                }
            }

            if (toGenerate == null) return;

            // ONE task for the whole batch: the generator parallelises across the glyphs it is given, so handing it the
            // batch keeps every core busy - the reason the caller pools a frame's text in the first place.
            System.Threading.Tasks.Task.Run(() =>
            {
                IReadOnlyList<GlyphTextureData> data;
                try
                {
                    data = atlasGenerator.GenerateTextureForGlyphs(toGenerate);
                }
                catch
                {
                    // A glyph that cannot be rasterized must not wedge the queue: let it out of flight and go on. The
                    // block that wanted it draws without it, exactly as it does for a glyph the font has no outline for.
                    lock (_asyncGate)
                    {
                        foreach (var glyph in toGenerate) _inFlight.Remove(glyph.Index);
                    }
                    return;
                }

                lock (_asyncGate)
                {
                    _ready.Enqueue(data);
                    foreach (var glyph in toGenerate) _generated[glyph.Index] = glyph;
                }
            });
        }

        /// <summary>Upload whatever the workers finished, on the thread that owns the device. Cheap - the expensive half
        /// (the MSDF itself) already happened elsewhere; measured at 12 ms against 650 for the generation. Returns true
        /// when something landed, which is the caller's cue that text built earlier is now out of date.</summary>
        public bool PumpReady()
        {
            IReadOnlyList<GlyphTextureData> batch = null;
            var landed = false;

            while (true)
            {
                lock (_asyncGate)
                {
                    if (_ready.Count == 0) break;
                    batch = _ready.Dequeue();
                }

                if (batch.Count > 0) ProcessTextureData(batch);

                lock (_asyncGate)
                {
                    foreach (var pair in _generated)
                    {
                        processedGlyphs[pair.Key] = pair.Value;
                        _inFlight.Remove(pair.Key);
                    }
                    _generated.Clear();
                }

                landed = true;
            }

            if (landed) Version++;
            return landed;
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