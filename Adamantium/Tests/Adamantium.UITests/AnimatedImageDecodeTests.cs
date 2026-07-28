using Adamantium.Imaging;
using Adamantium.UI.Core.Media.Imaging;
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

    /// <summary>
    /// Decoding frames is only half of it - the Image control walks them through BitmapImage, and the constructor that
    /// takes an already-decoded bitmap (a drop, a stream, anything that is not a URI) chains to base() rather than
    /// this(), so it used to leave the frame caches null. Every fetch threw, the control's catch swallowed it, and an
    /// animated picture sat on frame 0 for ever.
    /// </summary>
    [Test]
    public void BitmapImageFromDecodedBitmap_WalksItsFrames()
    {
        var raw = BitmapLoader.Load(TexturesDir + "infinity.gif");
        var bitmap = new BitmapImage(raw);
        bitmap.StartFrame = 0;
        bitmap.EndFrame = bitmap.FrameCount;

        Assert.That(bitmap.GetNextFrame(), Is.Not.Null, "first frame");
        Assert.That(bitmap.GetNextFrame(), Is.Not.Null, "second frame");
        Assert.That(bitmap.CurrentFrameIndex, Is.EqualTo(2), "playback must move off frame 0");
    }

    /// <summary>
    /// Decoding frames ahead is SYNCHRONOUS however async its name looks: it returns an already-completed Task, so an
    /// await on it continues inline on the calling thread. That is why playback must not do it - called from the thread
    /// that owns layout (any non-URI source sets Source there), a long animation froze the UI, scrolling included.
    /// </summary>
    [Test]
    public void DecodingFramesAhead_CompletesOnTheCallingThread()
    {
        var raw = BitmapLoader.Load(TexturesDir + "infinity.gif");
        var bitmap = new BitmapImage(raw);

        var task = bitmap.DecodeFramesTillAsync(bitmap.FrameCount);

        Assert.That(task.IsCompleted, Is.True, "it does the work inline - awaiting it moves nothing off this thread");
    }

    /// <summary>A frame nobody decoded ahead of time is decoded when it is ASKED for - what playback relies on.</summary>
    [Test]
    public void FramesDecodeOnDemand_WithoutDecodingAhead()
    {
        var raw = BitmapLoader.Load(TexturesDir + "infinity.gif");
        var bitmap = new BitmapImage(raw);

        var last = bitmap.GetFrame(bitmap.FrameCount - 1);

        Assert.That(last, Is.Not.Null, "the last frame, with nothing decoded before it");
    }

    /// <summary>Walking past the last frame comes back to the first rather than running off the end.</summary>
    [Test]
    public void BitmapImageFromDecodedBitmap_WrapsAtTheEnd()
    {
        var raw = BitmapLoader.Load(TexturesDir + "infinity.gif");
        var bitmap = new BitmapImage(raw);
        bitmap.StartFrame = 0;
        bitmap.EndFrame = bitmap.FrameCount;

        for (var i = 0; i < bitmap.FrameCount + 1; i++)
        {
            Assert.That(bitmap.GetNextFrame(), Is.Not.Null, $"frame {i}");
        }

        Assert.That(bitmap.CurrentFrameIndex, Is.EqualTo(1), "the loop restarts at the start frame");
    }
}
