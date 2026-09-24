using System;
using Adamantium.Core;
using NUnit.Framework;

namespace Adamantium.CoreTests;

[TestFixture]
public class SatellitesTests
{
    private interface IProbe
    {
    }

    private sealed class Probe : IProbe
    {
    }

    [Test]
    public void Get_ReturnsTheInstanceThatWasAdded()
    {
        var satellites = new Satellites();
        var probe = new Probe();

        satellites.Add<IProbe>(probe);

        Assert.That(satellites.Get<IProbe>(), Is.SameAs(probe));
    }

    [Test]
    public void Get_Throws_WhenNothingWasAdded()
    {
        var satellites = new Satellites();

        Assert.Throws<InvalidOperationException>(() => satellites.Get<IProbe>());
    }

    [Test]
    public void Add_Throws_WhenTheTypeIsAlreadyThere()
    {
        var satellites = new Satellites();
        satellites.Add<IProbe>(new Probe());

        Assert.Throws<InvalidOperationException>(() => satellites.Add<IProbe>(new Probe()));
    }

    [Test]
    public void Add_Throws_OnNull()
    {
        var satellites = new Satellites();

        Assert.Throws<ArgumentNullException>(() => satellites.Add<IProbe>(null));
    }

    [Test]
    public void Get_FindsBySameTypeItWasAddedUnder_NotByItsRuntimeType()
    {
        var satellites = new Satellites();
        satellites.Add<IProbe>(new Probe());

        Assert.Throws<InvalidOperationException>(() => satellites.Get<Probe>());
    }

    [Test]
    public void TwoOwners_KeepTheirOwnSatellites()
    {
        var first = new Satellites();
        var second = new Satellites();
        var firstProbe = new Probe();
        var secondProbe = new Probe();

        first.Add<IProbe>(firstProbe);
        second.Add<IProbe>(secondProbe);

        Assert.That(first.Get<IProbe>(), Is.SameAs(firstProbe));
        Assert.That(second.Get<IProbe>(), Is.SameAs(secondProbe));
    }
}
