using System;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The payload contract behind drag-drop formats: what a package promises versus what it has actually produced.
/// Deferred rendering only pays off if NOTHING on the advertising path redeems a promise, so that is what is pinned
/// down here - the engine asks these questions on every mouse move of a drag.
/// </summary>
[TestFixture]
public class DataPackageFormatTests
{
    [Test]
    public void DeferredFormat_IsAdvertisedWithoutBeingProduced()
    {
        var produced = 0;
        var package = new DataPackage();
        package.SetDeferred(DataFormats.Files, () => { produced++; return new[] { @"C:\heavy.bin" }; });

        Assert.That(package.Contains(DataFormats.Files), Is.True, "the format must be on offer from the start");
        Assert.That(package.GetFormats(), Does.Contain(DataFormats.Files));
        Assert.That(package.IsDeferred(DataFormats.Files), Is.True);
        Assert.That(produced, Is.Zero, "advertising a format must not produce it");
    }

    [Test]
    public void ReadingADeferredFormat_ProducesItExactlyOnce()
    {
        var produced = 0;
        var package = new DataPackage();
        package.SetDeferred(DataFormats.Text, () => { produced++; return "expensive"; });

        Assert.That(package.Get(DataFormats.Text), Is.EqualTo("expensive"));
        Assert.That(package.Get(DataFormats.Text), Is.EqualTo("expensive"));

        Assert.That(produced, Is.EqualTo(1), "a target may ask twice; the payload must be rendered once");
        Assert.That(package.IsDeferred(DataFormats.Text), Is.False, "a redeemed promise is no longer deferred");
    }

    // The live-object questions ("is this drag carrying a MyItem?") run on every move while a drag is over a target.
    // Redeeming a promise to answer them would turn deferred rendering into eager rendering with extra steps.
    [Test]
    public void TypedLookups_DoNotRedeemAPromise()
    {
        var produced = 0;
        var package = new DataPackage();
        package.SetDeferred("application/x-heavy", () => { produced++; return "text that would match"; });

        Assert.That(package.Get<string>(), Is.Null);
        Assert.That(package.Contains<string>(), Is.False);
        Assert.That(produced, Is.Zero);
    }

    [Test]
    public void SetDeferred_ReplacesAnEarlierValueUnderTheSameName()
    {
        var package = new DataPackage();
        package.Set(DataFormats.Text, "eager");
        package.SetDeferred(DataFormats.Text, () => "lazy");

        Assert.That(package.IsDeferred(DataFormats.Text), Is.True);
        Assert.That(package.Get(DataFormats.Text), Is.EqualTo("lazy"));
    }

    [Test]
    public void SetDeferred_RejectsANullProducer()
    {
        var package = new DataPackage();
        Assert.Throws<ArgumentNullException>(() => package.SetDeferred(DataFormats.Text, null));
    }
}
