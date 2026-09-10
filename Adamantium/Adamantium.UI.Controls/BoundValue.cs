using System;
using System.Globalization;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>One value, produced by a real <see cref="BindingBase"/> against one object - the ONLY way an inspector row
/// or a grid cell reads and writes a value. The declared binding is cloned per object and applied here, so converters,
/// <c>StringFormat</c>, <c>MultiBinding</c> and validation all come with it, which a member name could not carry.
/// <para>A <see cref="FundamentalUIComponent"/> so it can join the row's logical tree - a binding that reads an
/// ancestor then resolves like any other.</para></summary>
internal sealed class BoundValue : FundamentalUIComponent
{
    /// <summary>Two-way by default, so an editor writing here reaches the object without every binding in every
    /// inspector having to spell <c>Mode=TwoWay</c>.</summary>
    public static readonly AdamantiumProperty ValueProperty = AdamantiumProperty.Register(nameof(Value),
        typeof(object), typeof(BoundValue),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    private bool _writing;
    private bool _writable;

    /// <summary>Raised when the SOURCE moved the value - not when we wrote it ourselves.</summary>
    public event EventHandler Changed;

    public object Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Points this at one object through the declared binding. The binding is CLONED: the same declaration
    /// serves every selected object and every row of a table at once, and one shared expression could not.</summary>
    public void PointAt(object source, BindingBase declared)
    {
        BindingEngine.ClearBindings(this);

        // CLEARED, not assigned null: a written value is LOCAL and outranks the binding's slot, so it would shadow
        // every value the new binding produces. BOTH slots - a binding whose source value is null leaves the target
        // alone rather than clobbering it, so a reused carrier keeps what the LAST object produced.
        ClearValue(ValueProperty);
        ClearValue(ValueProperty, ValuePriority.Binding);
        _writable = false;

        if (declared == null || source == null) return;

        var binding = (BindingBase)declared.Clone();

        // A plain {Binding Member} means "off the object being inspected". One that names its own source, element or
        // ancestor said where to look, and is left exactly as written.
        if (binding is Binding { Source: null, ElementName: null } plain) plain.Source = source;

        // A binding written OneWay is a value to look at - Default is two-way here, because this property says so. Saying
        // "written" when nothing was written would leave the inspector showing a number the object never took.
        _writable = binding is not Binding { Mode: BindingMode.OneWay or BindingMode.OneTime };

        // Synchronous, unlike an ordinary target binding: a source change is normally coalesced to once per frame, and
        // the frame-final value is exactly right for something being DRAWN. This is not being drawn - it is what a row
        // is asked for, in the same breath it is pointed at an object, with no frame in between.
        binding.IsImmediate = true;

        BindingEngine.SetBinding(this, ValueProperty, binding);
    }

    /// <summary>Writes through the binding to the object. False when the binding cannot write, or when what an editor
    /// produced will not fit what the object holds - a refusal, which is what validation looks like from here.</summary>
    public bool Write(object value)
    {
        if (!_writable) return false;
        if (!TryConvert(value, Value?.GetType(), out value)) return false;

        _writing = true;
        try
        {
            Value = value;
        }
        finally
        {
            _writing = false;
        }

        return true;
    }

    public void Release() => BindingEngine.ClearBindings(this);

    /// <summary>Turns what an editor produced into what the object will take - a string into a number, a chosen item
    /// into its underlying value. False refuses, and refusing is the point: a value that will not fit must not be
    /// reported as written.</summary>
    internal static bool TryConvert(object edited, Type target, out object value)
    {
        value = edited;
        if (target == null || edited == null) return true;
        if (target.IsInstanceOfType(edited)) return true;

        var underlying = Nullable.GetUnderlyingType(target) ?? target;

        if (underlying.IsEnum)
        {
            if (!Enum.TryParse(underlying, edited.ToString(), ignoreCase: true, out var parsed)) return false;

            value = parsed;
            return true;
        }

        // The USER'S culture first - "2,5" is what a Russian keyboard produces and what the editor showed him - and the
        // invariant one after it, because a value that came from a file or from code is written "2.5" whatever the
        // machine is set to. Trying only one of the two makes half the sources unparsable.
        return Convert(edited, underlying, CultureInfo.CurrentCulture, out value)
               || Convert(edited, underlying, CultureInfo.InvariantCulture, out value);
    }

    private static bool Convert(object edited, Type target, IFormatProvider culture, out object value)
    {
        try
        {
            value = System.Convert.ChangeType(edited, target, culture);
            return true;
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            value = edited;
            return false;
        }
    }

    private static void OnValueChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is BoundValue { _writing: false } bound) bound.Changed?.Invoke(bound, EventArgs.Empty);
    }
}
