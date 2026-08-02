using System.Diagnostics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// What a property READ costs at the scale the engine actually runs at - 60k live components, each read several times a
/// frame by measure and arrange. Explicit: it is a measurement, not a pass/fail, and it has no business slowing the
/// suite down. Run it with --filter FullyQualifiedName~PropertyReadBenchmark.
/// </summary>
[TestFixture, Explicit("A measurement, not an assertion - run it by name.")]
public class PropertyReadBenchmark
{
    private const int Components = 60_000;
    private const int Passes = 20;

    [Test]
    public void ReadingSixtyThousandComponents()
    {
        var borders = new Border[Components];
        for (var i = 0; i < Components; i++) borders[i] = new Border();

        // Warm up the JIT and the caches, so the numbers are about the read path and not about the first touch.
        var warm = 0.0;
        for (var pass = 0; pass < 3; pass++)
            for (var i = 0; i < Components; i++) warm += Read(borders[i]);

        var lockFree = Stopwatch.StartNew();
        var sum = 0.0;
        for (var pass = 0; pass < Passes; pass++)
            for (var i = 0; i < Components; i++) sum += Read(borders[i]);
        lockFree.Stop();

        // The same reads, each behind an UNCONTENDED lock - what the old path paid on top of the read itself, before the
        // container stopped mutating on read and the store stopped needing one.
        var gate = new object();
        var locked = Stopwatch.StartNew();
        for (var pass = 0; pass < Passes; pass++)
            for (var i = 0; i < Components; i++)
            {
                lock (gate) sum += Read(borders[i]);
            }
        locked.Stop();

        var reads = (long)Components * Passes * 6;
        TestContext.WriteLine($"components={Components:N0} reads={reads:N0} (6 properties x {Passes} passes)");
        TestContext.WriteLine($"lock-free : {lockFree.ElapsedMilliseconds,6} ms  ({reads / (double)lockFree.ElapsedMilliseconds / 1000:F1} M reads/s)");
        TestContext.WriteLine($"with lock : {locked.ElapsedMilliseconds,6} ms  ({reads / (double)locked.ElapsedMilliseconds / 1000:F1} M reads/s)");
        TestContext.WriteLine($"the lock cost {locked.ElapsedMilliseconds - lockFree.ElapsedMilliseconds} ms, " +
                              $"i.e. x{locked.ElapsedMilliseconds / (double)lockFree.ElapsedMilliseconds:F2}");
        TestContext.WriteLine($"(sum {sum:F0}, warm {warm:F0} - kept so nothing is optimised away)");
    }

    // What measure and arrange read off every node, every pass.
    private static double Read(Border border) =>
        border.Width + border.Height + border.MinWidth + border.MaxWidth + border.Opacity + (int)border.Visibility;
}
