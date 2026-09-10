using System.ComponentModel;
using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The builder that turns a TYPE into definitions. It is what makes an inspector work on an object nobody
/// wrote a form for - a component of an entity, say - and it is a class of its own so that an inspector written by hand
/// never drags reflection in with it.
/// <para>What it produces are ordinary definitions with ordinary bindings: nothing downstream can tell a generated
/// inspector from a written one.</para></summary>
[TestFixture]
public class PropertyDefinitionBuilderTests
{
    private sealed class Nested
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    private sealed class Component
    {
        [System.ComponentModel.Category("General")]
        public string Name { get; set; } = "one";

        [System.ComponentModel.Category("General")]
        [DisplayName("Is enabled")]
        public bool IsEnabled { get; set; }

        [System.ComponentModel.Category("Transform")]
        public int Order { get; set; }

        [System.ComponentModel.Category("Transform")]
        public Visibility Visibility { get; set; }

        [System.ComponentModel.Category("Transform")]
        public Nested Offset { get; set; } = new();

        public string ReadOnlyByLackOfSetter { get; } = "fixed";

        [Browsable(false)]
        public string Hidden { get; set; } = "not shown";
    }

    private static string PathOf(PropertyDefinition definition) => (definition?.Binding as Binding)?.Path?.Path;

    private static PropertyDefinition Definition(PropertyDefinitionBuilder builder, string path) =>
        builder.Build(typeof(Component)).FirstOrDefault(d => PathOf(d) == path);

    [Test]
    public void EachTypeGetsTheDefinitionThatFitsIt()
    {
        var builder = new PropertyDefinitionBuilder();

        Assert.Multiple(() =>
        {
            Assert.That(Definition(builder, "Name"), Is.InstanceOf<StringProperty>());
            Assert.That(Definition(builder, "IsEnabled"), Is.InstanceOf<BooleanProperty>());
            Assert.That(Definition(builder, "Order"), Is.InstanceOf<NumericProperty>());
            Assert.That(Definition(builder, "Visibility"), Is.InstanceOf<ChoiceProperty>());
            Assert.That(Definition(builder, "Offset"), Is.InstanceOf<CompositeProperty>(),
                "an object opens into properties of its own");
        });
    }

    // What the builder writes is a BINDING, not a member name - the same thing markup would have declared, so a
    // generated inspector can do everything a written one can.
    [Test]
    public void WhatItBuildsAreRealBindings()
    {
        var name = Definition(new PropertyDefinitionBuilder(), "Name");

        Assert.That(name.Binding, Is.InstanceOf<Binding>());
        Assert.That(((Binding)name.Binding).Path.Path, Is.EqualTo("Name"));
    }

    [Test]
    public void AnEnumPropertyKnowsItsOwnValues()
    {
        var choice = (ChoiceProperty)Definition(new PropertyDefinitionBuilder(), "Visibility");

        Assert.That(choice.EnumType, Is.EqualTo(typeof(Visibility)));
        Assert.That(choice.Choices(), Does.Contain(Visibility.Collapsed));
    }

    // A nested binding has to be qualified by the property that HOLDS it, or reading it off the target finds nothing:
    // "X" means nothing on the component, "Offset.X" is the value.
    [Test]
    public void NestedPropertiesCarryTheWholePath()
    {
        var composite = (CompositeProperty)Definition(new PropertyDefinitionBuilder(), "Offset");

        Assert.That(composite.Children.Select(PathOf), Is.EquivalentTo(new[] { "Offset.X", "Offset.Y" }));
    }

    [Test]
    public void ADepthOfZeroStopsBeforeOpeningObjects()
    {
        var builder = new PropertyDefinitionBuilder { MaxDepth = 0 };
        var definition = Definition(builder, "Offset");

        Assert.Multiple(() =>
        {
            Assert.That(definition, Is.Not.InstanceOf<CompositeProperty>(), "not opened");
            Assert.That(definition.IsReadOnly, Is.True, "and shown as text nobody can edit, rather than left out");
        });
    }

    [Test]
    public void WhatCannotBeWrittenIsReadOnly()
    {
        Assert.That(Definition(new PropertyDefinitionBuilder(), "ReadOnlyByLackOfSetter").IsReadOnly, Is.True,
            "a property with no setter must not offer an edit that cannot land");
    }

    [Test]
    public void WhatTheTypeHidesIsNotShown()
    {
        Assert.That(Definition(new PropertyDefinitionBuilder(), "Hidden"), Is.Null);
    }

    [Test]
    public void NamesAreMadeReadable()
    {
        var builder = new PropertyDefinitionBuilder();

        Assert.Multiple(() =>
        {
            Assert.That(Definition(builder, "IsEnabled").Header, Is.EqualTo("Is enabled"), "[DisplayName] wins");
            Assert.That(Definition(builder, "ReadOnlyByLackOfSetter").Header, Is.EqualTo("Read Only By Lack Of Setter"),
                "and without one the name is spaced out, as every inspector shows it");
        });
    }

    [Test]
    public void CategoriesBecomeSections()
    {
        var sections = new PropertyDefinitionBuilder().BuildSections(typeof(Component));

        Assert.That(sections.Select(s => s.Category), Does.Contain("General").And.Contain("Transform"));

        var general = sections.First(s => s.Category == "General");
        Assert.That(general.Properties.Select(PathOf), Is.EquivalentTo(new[] { "Name", "IsEnabled" }));
    }

    // One section per object is the shape an entity inspector has: a component is a section, its properties are lines.
    // The builder knows nothing about entities - it is handed objects.
    [Test]
    public void OneSectionPerObject()
    {
        var sections = new PropertyDefinitionBuilder().BuildSections(new object[] { new Component(), new Nested() });

        Assert.Multiple(() =>
        {
            Assert.That(sections.Count, Is.EqualTo(2));
            Assert.That(sections[0].Header, Is.EqualTo("Component"));
            Assert.That(sections[1].Header, Is.EqualTo("Nested"));
            Assert.That(sections[1].Target, Is.InstanceOf<Nested>(), "each section inspects its own object");
        });
    }
}
