using Adamantium.Win32;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Reading the operating system's appearance. There is no API for the Windows setting, so it is read out of the
/// registry through a platform call - and a platform call is exactly the kind of thing that compiles perfectly and
/// then marshals wrongly at runtime.
/// </summary>
[TestFixture]
public class SystemAppearanceReadTests
{
    [Test]
    public void TheAppearanceCanBeRead_WithoutThrowing()
    {
        // The value itself is whatever this machine is set to, so it cannot be asserted. What CAN be asserted is that
        // the call completes - a wrong signature on RegGetValueW would corrupt the stack or throw here rather than
        // quietly returning a plausible-looking false.
        Assert.DoesNotThrow(() => Win32Interop.SystemPrefersDarkAppearance());
    }

    [Test]
    public void ReadingItTwiceGivesTheSameAnswer()
    {
        // Marshalling a by-ref size that the call REWRITES is the usual way this goes wrong: the first read succeeds,
        // the second is handed a size of zero and fails. Asking twice is what catches it.
        var first = Win32Interop.SystemPrefersDarkAppearance();
        var second = Win32Interop.SystemPrefersDarkAppearance();

        Assert.That(second, Is.EqualTo(first));
    }
}
