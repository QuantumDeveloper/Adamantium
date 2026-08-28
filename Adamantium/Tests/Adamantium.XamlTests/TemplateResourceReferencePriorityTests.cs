using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A <c>{ResourceReference}</c> must rank exactly where a LITERAL in the same position ranks - inside a control
/// template it is the template's value, written straight onto an element it is that element's own.
/// <para>It used not to: the literal path already asked for Template priority inside a template, but the reference path
/// went through <see cref="ResourceResolver.SetDeferred"/>, which used the default - and the default is LOCAL, which
/// OUTRANKS Trigger. So a metric and a number in the SAME attribute behaved differently, and the difference only showed
/// as a trigger that quietly stopped working. Found on a vertical slider: the orientation trigger could not turn the
/// handle, size the fill or reverse the track, because all three had just been moved onto metrics - the very change
/// every theme is being asked to make.</para>
/// </summary>
[TestFixture]
public class TemplateResourceReferencePriorityTests
{
    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        UIAppContext.Initialize(new FakeApp(new AdamantiumDependencyContainer()), null);
    }

    /// <summary>The precedence the whole thing rests on: a template states the default look, a trigger states what a
    /// STATE does to it, and an element's own value outranks both.</summary>
    [Test]
    public void ThePrecedenceIsLocalThenTriggerThenTemplate()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)ValuePriority.Local, Is.LessThan((int)ValuePriority.Trigger));
            Assert.That((int)ValuePriority.Trigger, Is.LessThan((int)ValuePriority.Template));
        });
    }

    [Test]
    public void InsideATemplate_AReferenceLosesToATrigger()
    {
        var part = new Border();

        ResourceResolver.SetDeferred(part, nameof(Border.Width), "AMissingMetric", ValuePriority.Template);
        part.SetValue(nameof(Border.Width), 42.0, ValuePriority.Trigger);

        Assert.That(part.Width, Is.EqualTo(42.0),
            "a state has to be able to change a part the template sized from a metric");
    }

    /// <summary>The other half, and the reason this is a parameter rather than a constant: a reference written straight
    /// onto an element is that element's OWN value and keeps outranking a trigger, exactly as a literal there would.
    /// Making the resolver always say Template would have quietly demoted every such value in every view.</summary>
    [Test]
    public void OnAnElement_AReferenceStillOutranksATrigger()
    {
        var element = new Border();

        ResourceResolver.SetDeferred(element, nameof(Border.Width), "AMissingMetric");
        element.SetValue(nameof(Border.Width), 7.0, ValuePriority.Local);
        element.SetValue(nameof(Border.Width), 42.0, ValuePriority.Trigger);

        Assert.That(element.Width, Is.EqualTo(7.0), "what the author wrote on the element wins");
    }
}
