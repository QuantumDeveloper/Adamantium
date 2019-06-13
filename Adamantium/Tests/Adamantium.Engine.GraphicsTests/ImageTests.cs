using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Imaging.JPEG;
using Adamantium.Engine.Graphics.Imaging.JPEG.Decoder;
using Adamantium.Engine.Graphics.Imaging.JPEG.Encoder;
using Adamantium.Engine.Graphics.Imaging.PNG;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Image = Adamantium.Engine.Graphics.Image;

namespace Adamantium.Engine.GraphicsTests
{
    [TestFixture]
    public class ImageTests
    {
        [Test]
        public void DDSImageTest()
        {
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\balls.dds");
            img.Save("loaded.dds", ImageFileType.Dds);
            img.Dispose();

            img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\TextureCube.dds");
            img.Save("TextureCube.dds", ImageFileType.Dds);
            img.Dispose();
        }

        [Test]
        public void TGAImageTest()
        {
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\2RLEExpand.tga");
            img.Save("2RLEExpand_reconstructed.tga", ImageFileType.Tga);
            img.Dispose();

            img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\luxfon.tga");
            img.Save("luxfon_reconstructed.tga", ImageFileType.Tga);
            img.Dispose();
        }

        [Test]
        public void BitmapImageTest()
        {
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\AplhaTestBitmap.bmp");
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\AplhaTestBitmap_24.bmp");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Shapes.bmp");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Small_24.bmp");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\t2_24.bmp");
            img.Save(@"BaseAlbedoTexture_Text.jpg", ImageFileType.Jpg);
            img.Save(@"BaseAlbedoTexture_Text.bmp", ImageFileType.Bmp);
            img?.Dispose();
        }

        [Test]
        public void IcoImageTest()
        {
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\SharpGen.ico");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\Testicon24.ico");
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\Testicon32.ico");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\NewIcon.ico");

            img.Save(@"RestoredBitmap.bmp", ImageFileType.Bmp);
            img.Save(@"RestoredBitmap.jpg", ImageFileType.Jpg);
            img.Save(@"RestoredBitmap.dds", ImageFileType.Dds);
            img?.Dispose();
        }

        [Test]
        public void JpegImageTest()
        {
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\luxfon.jpg");
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\testpng1.jpg");
            img?.Save("luxfon.bmp", ImageFileType.Bmp);
            img?.Save("luxfon.tga", ImageFileType.Tga);
            img?.Save("luxfon.jpg", ImageFileType.Jpg);

            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\SharpGen.ico");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\1.jpg");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\SharpGen.ico");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\NewIcon.ico");

            //img.Save(@"RestoredBitmap.bmp", ImageFileType.Bmp);
            //img.Save(@"RestoredBitmap.tga", ImageFileType.Tga);
            img?.Dispose();
        }

        [Test]
        public void PngImageTest()
        {
            //PNGCompressor compressor = new PNGCompressor();
            //string str = "The novel begins in July 1805 in Saint Petersburg, at a soirée given by Anna Pavlovna Scherer—the maid of honour and confidante to the dowager Empress Maria Feodorovna. Many of the main characters are introduced as they enter the salon. Pierre (Pyotr Kirilovich) Bezukhov is the illegitimate son of a wealthy count, who is dying after a series of strokes. Pierre is about to become embroiled in a struggle for his inheritance. Educated abroad at his father's expense following his mother's death, Pierre is kindhearted but socially awkward, and finds it difficult to integrate into Petersburg society. It is known to everyone at the soirée that Pierre is his father's favorite of all the old count’s illegitimate progeny."
            //+"Also attending the soirée is Pierre's friend, Prince Andrei Nikolayevich Bolkonsky, husband of Lise, a charming society favourite. He is disillusioned with Petersburg society and with married life, feeling that his wife is empty and superficial, he comes to hate her and all women, expressing patently misogynistic views to Pierre when the two are alone. Pierre doesn't quite know what to do with this, and is made uncomfortable witnessing the marital discord. Andrei tells Pierre he has decided to become aide - de - camp to Prince Mikhail Ilarionovich Kutuzov in the coming war against Napoleon in order to escape a life he can't stand."
            //+"The plot moves to Moscow, Russia's former capital, contrasting its provincial, more Russian ways to the more European society of Saint Petersburg. The Rostov family are introduced. Count Ilya Andreyevich Rostov and Countess Natalya Rostova are an affectionate couple but forever worried about their disordered finances. They have four children. Thirteen-year-old Natasha (Natalia Ilyinichna) believes herself in love with Boris Drubetskoy, a young man who is about to join the army as an officer. Twenty-year-old Nikolai Ilyich pledges his love to Sonya (Sofia Alexandrovna), his fifteen-year-old cousin, an orphan who has been brought up by the Rostovs. The eldest child, Vera Ilyinichna, is cold and somewhat haughty but has a good prospective marriage in a Russian-German officer, Adolf Karlovich Berg. Petya (Pyotr Ilyich) at nine is the youngest; like his brother, he is impetuous and eager to join the army when of age.";
            //var bytes = Encoding.ASCII.GetBytes(str);
            //var settings = new PNGEncoderSettings();
            //settings.UseLZ77 = false;
            //settings.BType = 2;
            //var lst = new List<byte>();
            //var error = compressor.Compress(bytes, settings, lst);
            //var decoderSettings = new PNGDecoderSettings();
            //var decompressedLst = new List<byte>();
            //error = compressor.Decompress(lst.ToArray(), decoderSettings, decompressedLst);
            //var decompressedStr = Encoding.ASCII.GetString(decompressedLst.ToArray());

            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\BaseAlbedoTexture_Text.png");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\testpng1.png");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\testpng6.png");

            //var bytes = File.ReadAllBytes(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\converted2.png");
            //var img = Image.New2D(1920, 1080, 1, SurfaceFormat.R8G8B8A8.UNorm);
            //var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            //Utilities.CopyMemory(img.DataPointer, handle.AddrOfPinnedObject(), bytes.Length);
            //handle.Free();
            var timer = Stopwatch.StartNew();
            //img?.Save("1.bmp", ImageFileType.Bmp);
            img?.Save("1.png", ImageFileType.Png);
            timer.Stop();
            //img?.Save("1.tga", ImageFileType.Tga);
            //img?.Save("1.dds", ImageFileType.Dds);
            //img?.Save("1.jpg", ImageFileType.Jpg);


            img?.Dispose();
        }

        [Test]
        public void GIFImageTest()
        {
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Rotating_earth.gif");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\225px-GIF_-_bubble_animation.gif");
            //img?.Save("1.bmp", ImageFileType.Bmp);
            //img?.Save("1.tga", ImageFileType.Tga);
            //img?.Save("1.jpg", ImageFileType.Jpg);

            img?.Dispose();
        }
    }
}
