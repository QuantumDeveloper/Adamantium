using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Adamantium.Imaging;
using Adamantium.Imaging.PaletteQuantizer.Extensions;
using Adamantium.Mathematics;

namespace Adamantium.FontTests
{
    public class MSDFTests
    {
        [Test]
        public void MSDFGenerator()
        {
            //var t = TypeFace.LoadSystemFont("times", 3);
            var timer = Stopwatch.StartNew();
            var typeface = Typeface.LoadFont(@"OTFFonts/Crimson-Italic.otf", 3);
            var font = typeface.GetFont(0);
            uint mtsdfTextureSize = 64;
            byte sampleRate = 3;
            var atlasGen = new TextureAtlasGenerator(typeface, font, new FontAtlasData(64), FontParameters.Default());
            //var atlasData = atlasGen.GenerateTextureAtlas(typeface, font, mtsdfTextureSize, sampleRate, 4, 0, typeface.GlyphCount);
            var atlasData = atlasGen.PrepareTextureAtlas();
            
            timer.Stop();
            var timer2 = Stopwatch.StartNew();
            //var atlasData = atlasGen.GenerateTextureAtlas(typeface, font, mtsdfTextureSize, sampleRate, 4, 0, 10);
            var glyphs = font.TranslateIntoGlyphs("Hello string");
            glyphs = glyphs.Distinct(x=>x.Index).ToArray();
            var textureData = atlasGen.GenerateTextureForGlyphs(glyphs);
            timer2.Stop();
            
            var img = Image.New2D((uint)atlasData.AtlasSize.Width, (uint)atlasData.AtlasSize.Height, SurfaceFormat.R8G8B8A8.UNorm);
            var pixels = img.GetPixelBuffer(0, 0);
            pixels.SetPixels(atlasData.ImageData);
            img.Save("msdf.png", ImageFileType.Png);
            
            Assert.Pass($"Atlas data for {font.GlyphCount} was generated in {timer.ElapsedMilliseconds}ms");
        }
        
        [Test]
        public void MSDFGenerator2()
        {
            //var t = TypeFace.LoadSystemFont("times", 3);
            var timer = Stopwatch.StartNew();
            var typeface = Typeface.LoadFont(@"OTFFonts/Crimson-Italic.otf", 3);
            var font = typeface.GetFont(0);
            uint mtsdfTextureSize = 64;
            byte sampleRate = 3;
            var atlasData = new FontAtlasData(64, new Size(1024, 1024));
            var atlasGen = new TextureAtlasGenerator(typeface, font, atlasData, FontParameters.Default());
            //var atlasData = atlasGen.GenerateTextureAtlas(typeface, font, mtsdfTextureSize, sampleRate, 4, 0, typeface.GlyphCount);
            var glyphs = font.TranslateIntoGlyphs("Hello string");
            glyphs = glyphs.Distinct(x=>x.Index).ToArray();
            var textureData = atlasGen.GenerateTextureForGlyphs(glyphs);
            atlasGen.CopyTextureDataToImage(textureData);
            var img = Image.New2D((uint) atlasData.AtlasSize.Width, (uint)atlasData.AtlasSize.Height, SurfaceFormat.R8G8B8A8.UNorm);
            var pixels = img.GetPixelBuffer(0, 0);
            pixels.SetPixels(atlasData.ImageData);
            img.Save("msdf2.png", ImageFileType.Png);

            // DIAGNOSTIC: median (coverage) preview — exactly what the shader thresholds.
            // Solid glyph interior => white, exterior => black, edges => gray ramp.
            var med = (byte[])atlasData.ImageData.Clone();
            for (int i = 0; i < med.Length; i += 4)
            {
                byte r = atlasData.ImageData[i], g = atlasData.ImageData[i + 1], b = atlasData.ImageData[i + 2];
                byte m = System.Math.Max(System.Math.Min(r, g), System.Math.Min(System.Math.Max(r, g), b));
                med[i] = med[i + 1] = med[i + 2] = m;
                med[i + 3] = 255;
            }
            var medImg = Image.New2D((uint)atlasData.AtlasSize.Width, (uint)atlasData.AtlasSize.Height, SurfaceFormat.R8G8B8A8.UNorm);
            medImg.GetPixelBuffer(0, 0).SetPixels(med);
            medImg.Save("msdf_median.png", ImageFileType.Png);

            Assert.Pass($"Atlas data for {font.GlyphCount} was generated in {timer.ElapsedMilliseconds}ms");
        }

        [Test]
        public void TypeFaceSerializationTest()
        {
            var resolver = CompositeResolver.Create(
                new IMessagePackFormatter[] { TypelessFormatter.Instance },
                new IFormatterResolver[] { StandardResolverAllowPrivate.Instance });

            var options = MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(resolver);
            var stream = new MemoryStream();

            var timer = Stopwatch.StartNew();
            var typeface = Typeface.LoadFont(@"OTFFonts/SourceSans3-Regular.otf", 3);
            
            var result = MessagePackSerializer.Serialize<Typeface>(typeface, StandardResolverAllowPrivate.Options);
            timer.Stop();
            typeface.GetGlyphByIndex(150, out var glyph);
            //var result = MessagePackSerializer.Serialize<Glyph>(glyph, options);

            //MessagePackSerializer.Serialize(stream, typeface);
            //stream.Position = 0;
            //var typeface2 = MessagePackSerializer.Deserialize<TypeFace>(stream);
            var timer2 = Stopwatch.StartNew();
            var glyph2 = MessagePackSerializer.Deserialize<Glyph>(result, options);
            timer2.Stop();
            Debug.WriteLine("");
        }
    }
}
