using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;
using System.Collections.Generic;

namespace Adamantium.Engine.Graphics.Fonts
{
    public class FontAtlas : GraphicsResource
    {
        protected FontAtlasData AtlasData { get; }

        protected TypeFace TypeFace { get; }

        protected Texture Atlas { get; set; }

        private GlyphLayoutContainer layoutContainer;

        public FontAtlas(GraphicsDevice device, FontAtlasData atlasData) : base(device)
        {
            AtlasData = atlasData;
            TypeFace = TypeFace.LoadFont(AtlasData.FontData, 3);
            var description = new TextureDescription();
            description.Width = (uint)AtlasData.AtlasSize.Width;
            description.Height = (uint)AtlasData.AtlasSize.Height;
            description.Depth = 1;
            description.ArrayLayers = 1;
            description.MipLevels = 1;
            description.Format = Format.R8G8B8A8_UNORM;
            description.InitialLayout = ImageLayout.Preinitialized;
            description.ImageType = ImageType._2d;
            description.ImageAspect = ImageAspectFlagBits.ColorBit;
            Atlas = Texture.CreateFrom(GraphicsDevice, description, AtlasData.ImageData);
            layoutContainer = new GlyphLayoutContainer(TypeFace);
        }

        public Size MeasureString(string text, double fontSize, FontRenderingParameters renderingParameters)
        {
            var font = TypeFace.GetFont(0);
            var glyphs = font.TranslateIntoGlyphs(text);
            layoutContainer.AddGlyphs(glyphs);
            
            double penPosition = 0.0;
            var ascenderLineDiff = fontSize * (font.UnitsPerEm - font.Ascender) / font.UnitsPerEm;
            var glyphList = new List<SpriteBatchItem>();
            
            // try to apply GPOS kern
            var kernApplied = font.FeatureService.ApplyFeature(FeatureNames.kern, layoutContainer, 0, (uint)glyphs.Length);
            var subApp = font.FeatureService.ApplyFeature(FeatureNames.aalt, layoutContainer, 0, (uint)glyphs.Length);
            
            for (var i = 0; i < layoutContainer.Count; i++)
            {
                var glyph = layoutContainer.GetGlyph(i); // @TODO move everything inside Layout Container, and remove GetGlyph method
                glyph.CalculateEmRelatedMultipliers(font.UnitsPerEm); // @TODO: need to somehow calculate this at glyph creation time (for each glyph, maybe at parser)

                var positionMultiplier = glyph.EmRelatedCenterToBaseLineMultiplier;

                double left = penPosition - fontSize * positionMultiplier.X;

                // if GPOS kern is not applied - try TTF kern approach
                if (!kernApplied)
                {
                    if (i > 0)
                    {
                        Glyph prevGlyph = layoutContainer.GetGlyph(i - 1); // @TODO move everything inside Layout Container, and remove GetGlyph method
                        left += fontSize * font.GetKerningValue((ushort)prevGlyph.Index, (ushort)glyph.Index) / (double)font.UnitsPerEm;
                    }
                }

                double top = fontSize * positionMultiplier.Y - ascenderLineDiff;

                double right = left + fontSize;
                double bottom = top + fontSize;
                var fontItem = new SpriteBatchItem();
                fontItem.Destination = new Vector4F((float)left, (float)top, (float)(right - left), (float)(bottom - top));

                fontItem.Source= AtlasData.GetTextureAtlasUVCoordinates(glyph.Index);
                glyphList.Add(fontItem);
                
                penPosition += fontSize * glyph.EmRelatedAdvanceWidthMultiplier;

                // if GPOS kern is applied - modify the advance for current glyph
                if (kernApplied)
                {
                    penPosition += fontSize * layoutContainer.GetAdvance((uint)i).X / font.UnitsPerEm;
                }
            }
            
            return Size.Zero;
        }

        public void DrawString(string text, FontRenderingParameters parameters, RenderTarget renderTarget)
        {
            // GraphicsDevice.BasicEffect.Parameters["foregroundColor"].SetValue(material.AmbientColor);
            // GraphicsDevice.BasicEffect.Parameters["sampleType"].SetResource(smallGlyphTextureSampler);
            // GraphicsDevice.BasicEffect.Techniques["Basic"].Passes["SmallGlyph"].Apply();
        }
    }
}
