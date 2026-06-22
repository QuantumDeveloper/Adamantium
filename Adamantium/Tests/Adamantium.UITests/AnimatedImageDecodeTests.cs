using Adamantium.Imaging;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Sanity-checks that the multi-frame decoders actually report more than one frame for animated assets (APNG / GIF).
/// The Image control only starts its playback timer when FrameCount > 1, so this is the precondition for animation.
/// </summary>
[TestFixture]
public class AnimatedImageDecodeTests
{
    private const string TexturesDir = @"C:\AdamantiumEngine\Adamantium\Adamantium\Adamantium.Game.Sandbox\Textures\";

    [Test]
    public void Apng_DecodesMultipleFrames()
    {
        var raw = BitmapLoader.Load(TexturesDir + "APNG-cube.png");
        Assert.That(raw, Is.Not.Null, "APNG failed to load");
        TestContext.WriteLine($"APNG FramesCount = {raw.FramesCount}");
        Assert.That(raw.FramesCount, Is.GreaterThan(1), "APNG should decode multiple frames");
    }

    [Test]
    public void Gif_DecodesMultipleFrames()
    {
        var raw = BitmapLoader.Load(TexturesDir + "infinity.gif");
        Assert.That(raw, Is.Not.Null, "GIF failed to load");
        TestContext.WriteLine($"GIF FramesCount = {raw.FramesCount}");
        Assert.That(raw.FramesCount, Is.GreaterThan(1), "GIF should decode multiple frames");
    }
}
