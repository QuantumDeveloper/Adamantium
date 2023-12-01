using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Mathematics;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using NUnit.Framework;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Adamantium.Imaging;

namespace Adamantium.FontTests
{
    public class MSDFTests
    {
        [Test]
        public void MSDFGenerator()
        {
            //var t = TypeFace.LoadSystemFont("times", 3);
            var typeface = TypeFace.LoadFont(@"OTFFonts/SourceSans3-Regular.otf", 3);
            var font = typeface.GetFont(0);
            uint mtsdfTextureSize = 48;
            byte sampleRate = 10;
            var atlasGen = new TextureAtlasGenerator();
            var timer = Stopwatch.StartNew();
            var atlasData = atlasGen.GenerateTextureAtlas(typeface, font, mtsdfTextureSize, sampleRate, 4, 0, typeface.GlyphCount);
            //var atlasData = atlasGen.GenerateTextureAtlas(typeface, font, mtsdfTextureSize, sampleRate, 4, 0, 10);

            var img = Image.New2D((uint)atlasData.AtlasSize.Width, (uint)atlasData.AtlasSize.Height, SurfaceFormat.R8G8B8A8.UNorm);
            var pixels = img.GetPixelBuffer(0, 0);
            pixels.SetPixels(atlasData.ImageData);
            img.Save("msdf.png", ImageFileType.Png);

            timer.Stop();
            Assert.Pass($"Atlas data for {font.GlyphCount} was generated in {timer.ElapsedMilliseconds}ms");
        }

        [Test]
        public void DeserializeFontData()
        {
            var text = File.ReadAllText(@"OtfFontData.xml");
            var data = FontData.Parse(text);
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
            var typeface = TypeFace.LoadFont(@"OTFFonts/SourceSans3-Regular.otf", 3);
            
            var result = MessagePackSerializer.Serialize<TypeFace>(typeface, StandardResolverAllowPrivate.Options);
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
