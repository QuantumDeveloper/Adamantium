using System;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UITests.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>THE GROUND UNDER A PICTURE. An Image with no source drew nothing whatever - not a pixel - so a picture
/// being chosen, or loading, or named wrongly was a hole exactly the size of its slot, and what a person saw was a
/// selection frame around nothing.</summary>
[TestFixture]
public class ImageGroundTests
{
    private sealed class Recorder : IDrawingContext
    {
        public RecordingDrawingSession Session { get; } = new();

        public IDrawingSession ForControl(IUIComponent component) => Session;
    }

    // THROUGH A WINDOW, because Bounds - which is what the element draws itself into - is settled by a real layout
    // pass and not by calling Measure and Arrange by hand.
    private static RecordingDrawingSession Drawn(Image image)
    {
        var recorder = new Recorder();
        var window = new Window { Width = 400, Height = 300, Content = image };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);

        image.Render(recorder);

        return recorder.Session;
    }

    [Test]
    public void APictureWithNoFileDrawsItsGround()
    {
        var ground = new SolidColorBrush(Colors.Tomato);
        var drawn = Drawn(new Image { Background = ground, Width = 200, Height = 150 });

        Assert.That(drawn.Rectangles, Has.Count.EqualTo(1), "a picture with no file drew nothing at all");
        Assert.That(drawn.Rectangles[0].Brush, Is.SameAs(ground), "it drew something, but not with its own ground");
        Assert.That(drawn.Rectangles[0].Destination.Width, Is.EqualTo(200).Within(0.01),
            "the ground does not fill the element");
    }

    // NOTHING TO DRAW is still nothing: a ground is a brush somebody put there, and an Image without one draws what it
    // always drew.
    [Test]
    public void APictureWithNoGroundDrawsNothingAsBefore()
    {
        Assert.That(Drawn(new Image { Width = 200, Height = 150 }).Rectangles, Is.Empty);
    }

    // WHEN it is drawn is the property's to say, and all three answers mean what they say.
    [Test]
    public void TheGroundIsDrawnWhenTheStateSaysSo()
    {
        var ground = new SolidColorBrush(Colors.Tomato);

        Assert.Multiple(() =>
        {
            Assert.That(Drawn(new Image { Background = ground, BackgroundState = ImageBackgroundState.Never })
                .Rectangles, Is.Empty, "a ground that was told never to show showed");

            Assert.That(Drawn(new Image { Background = ground, BackgroundState = ImageBackgroundState.Always })
                .Rectangles, Has.Count.EqualTo(1), "a ground that was told always to show did not");

            Assert.That(Drawn(new Image { Background = ground, BackgroundState = ImageBackgroundState.WhenEmpty })
                .Rectangles, Has.Count.EqualTo(1), "an empty picture did not show its ground");
        });
    }

    // THE PICTURE PUT ASIDE. Switched off, the file and everything set about laying it out are kept - it is simply not
    // painted - and what is left is the ground. The two switches must not fight: a ground told "only while there is no
    // picture" has to count a switched-off one as none, or turning the foreground off would leave a blank element,
    // which is a switch that undoes itself.
    [Test]
    public void APictureCanBePutAsideAndLeaveItsGround()
    {
        var ground = new SolidColorBrush(Colors.Tomato);
        var image = new Adamantium.UI.Controls.Image
        {
            Background = ground,
            Source = new BitmapImage(new Uri("C:/no/such/folder/no-such-picture.png")),
            ShowsForeground = false,
            Width = 200,
            Height = 150
        };

        var drawn = Drawn(image);

        Assert.Multiple(() =>
        {
            Assert.That(drawn.Rectangles, Has.Count.EqualTo(1),
                "the ground did not appear when the picture was put aside");
            Assert.That(drawn.Rectangles[0].Brush, Is.SameAs(ground));
            Assert.That(image.Source, Is.Not.Null, "putting the picture aside threw the file away");
        });
    }

    // A PATH THAT NAMES NOTHING IS NOT A CRASH. The load runs on a thread pool thread out of an async void, so an
    // exception there reaches nobody and takes the PROCESS with it - and the path came from a person typing into a
    // panel. It killed the test host outright the first time a picture was pointed at a file that was not there.
    [Test]
    public void APathThatNamesNoFileLeavesTheApplicationStanding()
    {
        var image = new Image
        {
            Background = new SolidColorBrush(Colors.Tomato),
            Source = new BitmapImage(new Uri("C:/no/such/folder/no-such-picture.png")),
            Width = 200,
            Height = 150
        };

        Assert.DoesNotThrow(() =>
        {
            var drawn = Drawn(image);

            // ...and what it shows is what it shows before any file is chosen: the ground. A picture that failed and a
            // picture not yet chosen are the same thing to look at.
            Assert.That(drawn.Rectangles, Has.Count.EqualTo(1), "a picture that failed to load shows nothing at all");
        });

        // The failure is KEPT rather than swallowed: something has to be able to tell "did not load" from "still
        // coming" without watching the clock.
        var waited = (image.Source as BitmapImage)?.LoadTask;

        waited?.Wait(2000);

        Assert.That((image.Source as BitmapImage)?.LoadError, Is.Not.Null, "the file failed and nothing says why");
    }

    // ...AND IT IS ROUNDED like the picture is: a square ground under a rounded picture shows its own corners sticking
    // out at the four places the rounding was asked for.
    [Test]
    public void TheGroundTakesThePicturesCorners()
    {
        var image = new Image
        {
            Background = new SolidColorBrush(Colors.Tomato),
            CornerRadius = new CornerRadius(8),
            Width = 200,
            Height = 150
        };

        // The recorder keeps the rectangle it was given; what is being asked here is that the rounded overload is the
        // one called at all - the square one would have recorded the same rectangle and lost the corners silently.
        Assert.That(Drawn(image).Rectangles, Has.Count.EqualTo(1));
    }
}
