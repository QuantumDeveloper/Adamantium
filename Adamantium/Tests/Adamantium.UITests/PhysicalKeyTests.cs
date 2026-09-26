using Adamantium.Multiverse.Input;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Platforms.Windows;
using Adamantium.UI.Universes;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class PhysicalKeyTests
{
    [TestCase(0x11, false, Keys.W)]
    [TestCase(0x1E, false, Keys.A)]
    [TestCase(0x02, false, Keys.Digit1)]
    [TestCase(0x0B, false, Keys.Digit0)]
    [TestCase(0x1C, false, Keys.Enter)]
    [TestCase(0x1C, true, Keys.NumPadEnter)]
    [TestCase(0x4B, false, Keys.NumPad4)]
    [TestCase(0x4B, true, Keys.LeftArrow)]
    [TestCase(0x4C, false, Keys.NumPad5)]
    [TestCase(0x45, false, Keys.Pause)]
    [TestCase(0x45, true, Keys.NumLock)]
    [TestCase(0x1D, false, Keys.LeftControl)]
    [TestCase(0x1D, true, Keys.RightControl)]
    [TestCase(0x2A, false, Keys.LeftShift)]
    [TestCase(0x36, false, Keys.RightShift)]
    [TestCase(0x5B, true, Keys.LeftWindows)]
    [TestCase(0x3B, false, Keys.F1)]
    [TestCase(0x58, false, Keys.F12)]
    [TestCase(0x64, false, Keys.F13)]
    [TestCase(0x76, false, Keys.F24)]
    [TestCase(0x56, false, Keys.OemBackslash)]
    [TestCase(0x30, true, Keys.VolumeUp)]
    public void AWin32ScanCode_IsTheKeyInThatPlace(int scanCode, bool extended, Keys expected)
    {
        Assert.That(Win32ScanCodes.ToHidUsage(scanCode, extended), Is.EqualTo((uint)expected));
    }

    [Test]
    public void AnUnknownScanCode_SaysNothing()
    {
        Assert.That(Win32ScanCodes.ToHidUsage(0x00, false), Is.EqualTo(0u));
        Assert.That(Win32ScanCodes.ToHidUsage(0x7F, true), Is.EqualTo(0u));
    }

    [Test]
    public void ThePlace_WinsOverWhatTheLayoutMakesTheKeyType()
    {
        Assert.That(UIUniverseOutput.TryTranslate(Key.Z, (uint)Keys.W, out var key), Is.True);
        Assert.That(key, Is.EqualTo(Keys.W));
    }

    [Test]
    public void WithoutAPlace_TheVirtualKeyIsUsed()
    {
        Assert.That(UIUniverseOutput.TryTranslate(Key.A, 0, out var key), Is.True);
        Assert.That(key, Is.EqualTo(Keys.A));
    }

    [Test]
    public void APlaceTheEngineHasNoKeyFor_FallsBackToTheVirtualKey()
    {
        Assert.That(UIUniverseOutput.TryTranslate(Key.A, 0x0007_0087, out var key), Is.True);
        Assert.That(key, Is.EqualTo(Keys.A));
    }
}
