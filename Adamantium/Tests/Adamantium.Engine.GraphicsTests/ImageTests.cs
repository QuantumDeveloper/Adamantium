using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Imaging.JPEG;
using Adamantium.Engine.Graphics.Imaging.JPEG.Decoder;
using Adamantium.Engine.Graphics.Imaging.JPEG.Encoder;
using NUnit.Framework;
using System.IO;
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
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\SharpGen.ico");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\ai.ico");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\SharpGen.ico");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\NewIcon.ico");

            img.Save(@"RestoredBitmap.bmp", ImageFileType.Bmp);
            img.Save(@"RestoredBitmap.tga", ImageFileType.Tga);
            img?.Dispose();
        }

        [Test]
        public void JpegImageTest()
        {
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\luxfon.jpg");
            //img?.Save("1.bmp", ImageFileType.Bmp);
            img?.Save("1.tga", ImageFileType.Tga);
            img?.Save("1.jpg", ImageFileType.Jpg);

            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\SharpGen.ico");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\1.jpg");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\SharpGen.ico");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\Icons\NewIcon.ico");

            //img.Save(@"RestoredBitmap.bmp", ImageFileType.Bmp);
            //img.Save(@"RestoredBitmap.tga", ImageFileType.Tga);
            img?.Dispose();
        }
    }
}
