using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

// The store keeps whole views alive on purpose, so the limit is what stops a cache from being a leak - and it is a
// framework knob, which means an application may move it while views are already parked.
[TestFixture]
public class ParkedVisualsLimitTests
{
    [SetUp]
    [TearDown]
    public void ResetStore()
    {
        ParkedVisuals.Clear();
        ParkedVisuals.Limit = 20;
    }

    [Test]
    public void DefaultLimit_IsTwenty()
    {
        Assert.That(ParkedVisuals.Limit, Is.EqualTo(20));
    }

    [Test]
    public void Enabled_BeyondTheLimit_LetsTheOldestGo()
    {
        ParkedVisuals.Limit = 3;
        var held = TakeAll(Park(4, NavigationCacheMode.Enabled));

        Assert.That(held[0], Is.False, "the oldest must be the one let go");
        Assert.That(held[1] && held[2] && held[3], Is.True, "the rest must still be parked");
    }

    [Test]
    public void Required_IsNeverEvicted_HoweverManyArrive()
    {
        ParkedVisuals.Limit = 1;
        var held = TakeAll(Park(3, NavigationCacheMode.Required));

        Assert.That(held[0] && held[1] && held[2], Is.True,
            "Required is the answer that says 'never let this go' - the limit does not count it");
    }

    [Test]
    public void LoweringTheLimit_TrimsAtOnce()
    {
        var keys = Park(5, NavigationCacheMode.Enabled);

        ParkedVisuals.Limit = 2;
        var held = TakeAll(keys);

        Assert.That(held[0] || held[1] || held[2], Is.False,
            "an application that lowers the limit is asking for the memory back now, not at the next navigation");
        Assert.That(held[3] && held[4], Is.True, "the newest survive");
    }

    private static object[] Park(int count, NavigationCacheMode mode)
    {
        var keys = new object[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = new object();
            ParkedVisuals.Keep(keys[i], new ParkedView { Mode = mode });
        }

        return keys;
    }

    // Taking is how the store is asked, and it REMOVES - so every key is asked exactly once, in one pass.
    private static bool[] TakeAll(object[] keys)
    {
        var held = new bool[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            held[i] = ParkedVisuals.TryTake(keys[i], null, out _, out _, out _, out _);
        }

        return held;
    }

    private class ParkedView : Border
    {
        public NavigationCacheMode Mode { get; init; }

        public override NavigationCacheMode KeepAlive => Mode;
    }
}
