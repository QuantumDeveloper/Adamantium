using Adamantium.Engine;
using NUnit.Framework;

namespace Adamantium.EngineTests;

[TestFixture]
public class ObservatoryTests
{
    [Test]
    public void AFocusedOutputUnderThePointer_TakesKeyboardAndPointer()
    {
        var (universe, output, observatory) = Create();

        universe.Settle();

        Assert.That(observatory.KeyboardOutput, Is.SameAs(output));
        Assert.That(observatory.PointerOutput, Is.SameAs(output));
    }

    [Test]
    public void WhileTheSimulationIsPaused_NoOutputTakesInput()
    {
        var (universe, _, observatory) = Create();

        universe.IsSimulationPaused = true;
        universe.Settle();

        Assert.That(observatory.KeyboardOutput, Is.Null);
        Assert.That(observatory.PointerOutput, Is.Null);
        Assert.That(observatory.KeyboardInput, Is.Null);
        Assert.That(observatory.PointerInput, Is.Null);
    }

    [Test]
    public void WhenTheSimulationResumes_TheInputComesBack()
    {
        var (universe, output, observatory) = Create();
        universe.IsSimulationPaused = true;
        universe.Settle();

        universe.IsSimulationPaused = false;
        universe.Settle();

        Assert.That(observatory.KeyboardOutput, Is.SameAs(output));
        Assert.That(observatory.PointerOutput, Is.SameAs(output));
    }

    private static (TestUniverse Universe, TestOutput Output, Observatory Observatory) Create()
    {
        var universe = new TestUniverse();
        var output = new TestOutput();
        universe.Add(output);
        return (universe, output, new Observatory(universe, universe.EntityWorld));
    }
}
