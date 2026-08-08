using System;
using System.Collections.Generic;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>What a type's <c>OverrideMetadata</c> MEANS: an override states only what it changes, and everything it
/// stays silent about keeps coming from the metadata it overrides.</summary>
[TestFixture]
public class PropertyMetadataMergeTests
{
    private class Base : AdamantiumComponent
    {
        public static readonly AdamantiumProperty ValueProperty = AdamantiumProperty.Register(nameof(Value),
            typeof(double), typeof(Base),
            new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsMeasure, OnValueChanged, CoerceValue));

        public static readonly List<string> Log = [];

        protected static void OnValueChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e) =>
            Log.Add("base");

        private static object CoerceValue(AdamantiumComponent a, object value) => value is double d && d < 0 ? 0.0 : value;

        public double Value
        {
            get => GetValue<double>(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }

    // Moves ONLY the default. Everything else must survive: the option, the coercion, and the base's callback.
    private class MovesTheDefault : Base
    {
        static MovesTheDefault() => ValueProperty.OverrideMetadata(typeof(MovesTheDefault), new PropertyMetadata(100.0));
    }

    // Adds its OWN callback on top of the base's.
    private class AddsACallback : Base
    {
        static AddsACallback() =>
            ValueProperty.OverrideMetadata(typeof(AddsACallback), new PropertyMetadata(5.0, OnDerivedChanged));

        private static void OnDerivedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e) =>
            Log.Add("derived");
    }

    // RE-STATES the base's callback. It must still run ONCE per change, not twice.
    private class RestatesTheBaseCallback : Base
    {
        static RestatesTheBaseCallback() =>
            ValueProperty.OverrideMetadata(typeof(RestatesTheBaseCallback), new PropertyMetadata(7.0, OnValueChanged));
    }

    // Three levels, each stating one thing.
    private class Middle : Base
    {
        static Middle() => ValueProperty.OverrideMetadata(typeof(Middle), new PropertyMetadata(20.0));
    }

    private class Leaf : Middle
    {
        static Leaf() => ValueProperty.OverrideMetadata(typeof(Leaf), new PropertyMetadata(30.0, OnLeafChanged));

        private static void OnLeafChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e) => Log.Add("leaf");
    }

    [SetUp]
    public void Reset() => Base.Log.Clear();

    // Constructing is what runs a type's static initializer (the property map does it while flattening the type's
    // registrations), so a test that only READS metadata has to build one first or it reads the base's.
    private static T Built<T>() where T : Base, new()
    {
        var instance = new T();
        Base.Log.Clear();
        return instance;
    }

    [Test]
    public void AnOverrideThatOnlyMovesTheDefault_KeepsTheOptionsTheCoercionAndTheCallback()
    {
        var instance = Built<MovesTheDefault>();
        var metadata = Base.ValueProperty.GetDefaultMetadata(typeof(MovesTheDefault));

        Assert.Multiple(() =>
        {
            Assert.That(metadata.DefaultValue, Is.EqualTo(100.0), "the one thing it stated");
            Assert.That(metadata.AffectsMeasure, Is.True, "the option was not stated, so it comes from the base");
            Assert.That(metadata.CoerceValueCallback, Is.Not.Null, "and so does the coercion");
            Assert.That(metadata.PropertyChangedCallback, Is.Not.Null, "and so does the callback");
        });

        instance.Value = -5;
        Assert.That(instance.Value, Is.EqualTo(0.0), "the inherited coercion still runs");
    }

    [Test]
    public void ADerivedCallback_RunsAfterTheBaseOne_NotInsteadOfIt()
    {
        var instance = Built<AddsACallback>();
        instance.Value = 42;

        Assert.That(Base.Log, Is.EqualTo(new[] { "base", "derived" }),
            "base first: it sets up whatever the derived one then reacts to");
    }

    [Test]
    public void AnOverrideThatRestatesTheBaseCallback_StillRunsItOnce()
    {
        var instance = Built<RestatesTheBaseCallback>();
        instance.Value = 42;

        Assert.That(Base.Log, Is.EqualTo(new[] { "base" }),
            "a handler runs once per change - combining it with itself would re-coerce and re-raise twice");
    }

    [Test]
    public void ThreeLevels_FoldBaseFirst()
    {
        var instance = Built<Leaf>();
        var metadata = Base.ValueProperty.GetDefaultMetadata(typeof(Leaf));
        Assert.That(metadata.DefaultValue, Is.EqualTo(30.0), "the most derived declaration wins the default");

        instance.Value = 42;

        Assert.That(Base.Log, Is.EqualTo(new[] { "base", "leaf" }),
            "the middle level stated no callback, so it contributes none");
    }

    // Seeding a property with its default is not a CHANGE. A callback fired there runs in the BASE constructor, so a
    // derived type's own fields are still null.
    [Test]
    public void SeedingTheDefault_DoesNotRunTheChangedCallback()
    {
        var instance = new AddsACallback();

        Assert.That(Base.Log, Is.Empty, "nothing changed - the property has always read as its default");

        instance.Value = 42;
        Assert.That(Base.Log, Is.EqualTo(new[] { "base", "derived" }), "a real change still runs it");
    }

    private class BoolDefault : AdamantiumComponent
    {
        public static readonly List<string> Log = [];

        public static readonly AdamantiumProperty FlagProperty = AdamantiumProperty.Register(nameof(Flag),
            typeof(bool), typeof(BoolDefault), new PropertyMetadata(false, OnFlagChanged));

        private static void OnFlagChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e) =>
            Log.Add($"{e.OldValue}->{e.NewValue}");

        public bool Flag
        {
            get => GetValue<bool>(FlagProperty);
            set => SetValue(FlagProperty, value);
        }
    }

    [Test]
    public void AFalseDefault_IsNotTreatedAsAChangeEither()
    {
        BoolDefault.Log.Clear();
        var instance = new BoolDefault();

        Assert.That(BoolDefault.Log, Is.Empty);

        instance.Flag = true;
        Assert.That(BoolDefault.Log, Is.EqualTo(new[] { "False->True" }),
            "and a real change runs it once, reporting the value the property actually had");
    }

    // A type may declare metadata once. Merging a second declaration would make the result depend on which static
    // constructor ran first, and would quietly swallow what is almost always a duplicate declaration.
    [Test]
    public void DeclaringMetadataTwiceForOneType_IsAnErrorThatNamesBoth()
    {
        Built<MovesTheDefault>();   // runs its static initializer, i.e. makes the first declaration

        var ex = Assert.Throws<InvalidOperationException>(() =>
            Base.ValueProperty.OverrideMetadata(typeof(MovesTheDefault), new PropertyMetadata(1.0)));

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("Value"), "names the property");
            Assert.That(ex.Message, Does.Contain(nameof(MovesTheDefault)), "and the type");
        });
    }
}
