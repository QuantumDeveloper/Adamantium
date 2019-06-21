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
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\APNG\APNG-cube.png");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\BaseAlbedoTexture_Text.png");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\1.png");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\testpng6.png");

            //var bytes = File.ReadAllBytes(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\converted2.png");
            //var img = Image.New2D(1920, 1080, 1, SurfaceFormat.R8G8B8A8.UNorm);
            //var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            //Utilities.CopyMemory(img.DataPointer, handle.AddrOfPinnedObject(), bytes.Length);
            //handle.Free();
            var timer = Stopwatch.StartNew();
            img?.Save("test.bmp", ImageFileType.Bmp);
            img?.Save("test.png", ImageFileType.Png);
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
