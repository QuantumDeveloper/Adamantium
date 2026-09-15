using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>One line of a <see cref="PropertyGrid"/>: a name on the left and a value on the right. The TYPE lives on
/// the PROPERTY rather than on a column - one line is a number, the next a check box - which is what separates an
/// inspector from a table.
/// <para>A definition DESCRIBES a line; <see cref="PropertyRow"/> draws it. One definition serves several targets at
/// once (that is multi-selection) and outlives every row made from it.</para>
/// <para>A <see cref="FundamentalUIComponent"/> that JOINS the logical tree of its section, so <c>{Binding}</c> and
/// <c>{RelativeSource}</c> resolve here as on any element.</para></summary>
public abstract class PropertyDefinition : FundamentalUIComponent
{
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(PropertyDefinition), new PropertyMetadata(null));

    /// <summary>What this line reads and writes - a REAL binding against the object being inspected, with converters,
    /// formats, modes and <c>MultiBinding</c>. Not a member name, which can carry none of them.</summary>
    public static readonly AdamantiumProperty BindingProperty = AdamantiumProperty.Register(nameof(Binding),
        typeof(BindingBase), typeof(PropertyDefinition), new PropertyMetadata(null));

    public static readonly AdamantiumProperty DescriptionProperty = AdamantiumProperty.Register(nameof(Description),
        typeof(String), typeof(PropertyDefinition), new PropertyMetadata(null));

    public static readonly AdamantiumProperty IsReadOnlyProperty = AdamantiumProperty.Register(nameof(IsReadOnly),
        typeof(bool), typeof(PropertyDefinition), new PropertyMetadata(false));

    /// <summary>Whether the line is shown at all. Bindable like everything else here, which is what an inspector with a
    /// "show advanced" switch is made of.</summary>
    public static readonly AdamantiumProperty IsVisibleProperty = AdamantiumProperty.Register(nameof(IsVisible),
        typeof(bool), typeof(PropertyDefinition), new PropertyMetadata(true, OnIsVisibleChanged));

    /// <summary>Whether the line ends with the "..." button. OFF by default: a button on every line of an inspector is
    /// a column of buttons that mostly do nothing, and a line that offers one had better mean it.</summary>
    public static readonly AdamantiumProperty ShowActionButtonProperty = AdamantiumProperty.Register(
        nameof(ShowActionButton), typeof(bool), typeof(PropertyDefinition),
        new PropertyMetadata(false, OnShowActionButtonChanged));

    /// <summary>What the "..." button runs. The editor in the value cell says what the property IS; this is for
    /// everything a cell cannot hold - a file to pick, a longer form to open, a value to be computed from elsewhere -
    /// and what it does is the application's business, which is why it is a command and not an event on the control.
    /// </summary>
    public static readonly AdamantiumProperty ActionCommandProperty = AdamantiumProperty.Register(nameof(ActionCommand),
        typeof(ICommand), typeof(PropertyDefinition), new PropertyMetadata(null));

    /// <summary>What the command is given. Null means the objects the line is pointed at - which is what the command
    /// almost always wants, and what it would otherwise have to be handed by hand on every line.</summary>
    /// <summary>What this property is worth when nothing has been done to it. A row holding anything else says so and
    /// offers to put this back.
    /// <para>Set it and the property HAS a default, null included - which is why <see cref="HasDefault"/> is a flag of
    /// its own rather than a null check: null is a perfectly good default for a reference, and reading "no default" out
    /// of it would leave those rows unable to reset.</para></summary>
    // Starts UNSET, not null: null is a value a property can legitimately default to, and a slot starting at null
    // cannot tell being ASSIGNED null from never having been touched. AdamantiumProperty.UnsetValue is what the
    // property system already means by "nothing here", so assigning it back removes the default again.
    public static readonly AdamantiumProperty DefaultValueProperty = AdamantiumProperty.Register(nameof(DefaultValue),
        typeof(object), typeof(PropertyDefinition),
        new PropertyMetadata(AdamantiumProperty.UnsetValue, OnDefaultValueChanged));

    /// <summary>Whether a default was ever given. The definition keeps it; a row reads it to know whether resetting is
    /// something it can offer at all.</summary>
    public static readonly AdamantiumProperty HasDefaultProperty = AdamantiumProperty.Register(nameof(HasDefault),
        typeof(bool), typeof(PropertyDefinition), new PropertyMetadata(false));

    public static readonly AdamantiumProperty ActionCommandParameterProperty = AdamantiumProperty.Register(
        nameof(ActionCommandParameter), typeof(object), typeof(PropertyDefinition), new PropertyMetadata(null));

    /// <summary>The KEY of the picture on the action button, resolved live against the theme. Empty leaves the three
    /// dots the button wears by default.
    /// <para>Three dots mean "there is more" - a file to pick, a longer form to open. A button that DOES something on
    /// the spot has to look like the thing it does, or the first press is the way you find out: a dotted button that
    /// silently dropped a socket is not an inspector row, it is a trap.</para></summary>
    public static readonly AdamantiumProperty ActionIconProperty = AdamantiumProperty.Register(nameof(ActionIcon),
        typeof(String), typeof(PropertyDefinition), new PropertyMetadata(null));

    /// <summary>What the action button says it will do, on hover. "More" is what an undecorated one says, and it is a
    /// promise the button has to keep.</summary>
    public static readonly AdamantiumProperty ActionTipProperty = AdamantiumProperty.Register(nameof(ActionTip),
        typeof(String), typeof(PropertyDefinition), new PropertyMetadata(null));

    /// <summary>What the value looks like when nobody is editing it. Null means the default look for its kind.</summary>
    public static readonly AdamantiumProperty ValueTemplateProperty = AdamantiumProperty.Register(nameof(ValueTemplate),
        typeof(DataTemplate), typeof(PropertyDefinition), new PropertyMetadata(null));

    /// <summary>What it turns into while it IS being edited. Null means the default editor for its kind.</summary>
    public static readonly AdamantiumProperty EditorTemplateProperty = AdamantiumProperty.Register(
        nameof(EditorTemplate), typeof(DataTemplate), typeof(PropertyDefinition), new PropertyMetadata(null));

    protected PropertyDefinition()
    {
        Children.CollectionChanged += OnChildrenChanged;
    }

    /// <summary>Raised when something a row has to redraw itself for changed - visibility, or the children collection.</summary>
    internal event EventHandler LayoutChanged;

    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public BindingBase Binding
    {
        get => GetValue<BindingBase>(BindingProperty);
        set => SetValue(BindingProperty, value);
    }

    /// <summary>One line about what the property means - a tooltip, and the status line in an editor.</summary>
    public String Description
    {
        get => GetValue<String>(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool IsVisible
    {
        get => GetValue<bool>(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    public bool ShowActionButton
    {
        get => GetValue<bool>(ShowActionButtonProperty);
        set => SetValue(ShowActionButtonProperty, value);
    }

    public ICommand ActionCommand
    {
        get => GetValue<ICommand>(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public String ActionIcon
    {
        get => GetValue<String>(ActionIconProperty);
        set => SetValue(ActionIconProperty, value);
    }

    public String ActionTip
    {
        get => GetValue<String>(ActionTipProperty);
        set => SetValue(ActionTipProperty, value);
    }

    public object ActionCommandParameter
    {
        get => GetValue(ActionCommandParameterProperty);
        set => SetValue(ActionCommandParameterProperty, value);
    }

    public object DefaultValue
    {
        get
        {
            var value = GetValue(DefaultValueProperty);
            return ReferenceEquals(value, AdamantiumProperty.UnsetValue) ? null : value;
        }
        set => SetValue(DefaultValueProperty, value);
    }

    public bool HasDefault
    {
        get => GetValue<bool>(HasDefaultProperty);
        set => SetValue(HasDefaultProperty, value);
    }

    public DataTemplate ValueTemplate
    {
        get => GetValue<DataTemplate>(ValueTemplateProperty);
        set => SetValue(ValueTemplateProperty, value);
    }

    public DataTemplate EditorTemplate
    {
        get => GetValue<DataTemplate>(EditorTemplateProperty);
        set => SetValue(EditorTemplateProperty, value);
    }

    /// <summary>Properties nested under this one. A vector is one line with three children; a plain property has none.
    /// [Content], so they are written as the child elements they look like.</summary>
    [Content]
    public PropertyDefinitions Children { get; } = new();

    /// <summary>The editor this line puts in the value cell. Null means it cannot be edited through the UI.</summary>
    protected internal virtual DataTemplate DefaultEditorTemplate => null;

    /// <summary>What the value looks like when it CANNOT be edited. Null means plain text.</summary>
    protected internal virtual DataTemplate DefaultValueTemplate => null;

    protected internal DataTemplate EditorFor() => EditorTemplate ?? DefaultEditorTemplate;

    protected internal DataTemplate ValueFor() => ValueTemplate ?? DefaultValueTemplate;

    /// <summary>Fills the editor with the value. Called whenever the row is (re)bound - the editor is LIVE, so this
    /// runs on every refresh, not once at the start of an edit.</summary>
    protected internal virtual void PrepareEditor(IUIComponent editor, object value) { }

    /// <summary>Reads back what the editor holds. Null means "nothing to say" and the previous value stands.</summary>
    protected internal virtual object ReadEditor(IUIComponent editor) => null;

    /// <summary>Turns what the editor produced into what the property will take - a string into an int, a chosen item
    /// into its underlying value. Returns false to refuse, which leaves the row in edit with what was typed.</summary>
    protected internal virtual bool TryConvert(object edited, Type target, out object value) =>
        BoundValue.TryConvert(edited, target, out value);

    /// <summary>Whether two of the selected objects hold the SAME value, which is what decides that a row of a multiple
    /// selection shows a value rather than reporting that the objects disagree.
    /// <para><see cref="object.Equals(object, object)"/> by default, which is right for a number, a string or an enum.
    /// A value that is an OBJECT is a different matter: two brushes of the same colour are not the same instance, and
    /// comparing them by reference makes a row report a difference nobody can see. A definition whose value is an object
    /// says here what "the same" means for it - and does so WITHOUT the type itself gaining an equality, which the
    /// render cache's change detection depends on staying by reference.</para></summary>
    protected internal virtual bool SameValue(object left, object right) => Equals(left, right);

    /// <summary>Whether this property's editor can stand EMPTY - showing no value while staying usable, which is what a
    /// row of disagreeing objects needs: setting one value on all of them is the point of selecting several.
    /// <para>A field, a number and a list all have an empty state. A colour swatch does not - it is a colour or it is
    /// nothing - so a definition whose editor cannot show "no value" says so here, and its row stays blank instead of
    /// standing there showing a colour neither object holds.</para></summary>
    protected internal virtual bool EditorCanShowNothing => true;

    /// <summary>Writes what the editor produced INTO the value the property already holds, and says whether it did.
    /// <para>A property normally means "the value is replaced", and that is what happens when this says no. The
    /// exception is a value that is an OBJECT WITH PARTS and is shared: the object keeps the same brush and the brush
    /// changes colour, so everything else painting with that brush follows. Replacing it would leave them all on the
    /// old one.</para></summary>
    protected internal virtual bool WriteInto(object current, object edited) => false;

    private static void OnIsVisibleChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as PropertyDefinition)?.LayoutChanged?.Invoke(component, EventArgs.Empty);

    // The row reads this when it is bound, so a line that gains its button while the inspector is open has to be told
    // to look again - the same reason IsVisible says so.
    private static void OnShowActionButtonChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as PropertyDefinition)?.LayoutChanged?.Invoke(component, EventArgs.Empty);

    // Being ASSIGNED is what gives a property a default, whatever it was assigned - null included.
    private static void OnDefaultValueChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not PropertyDefinition definition) return;

        definition.HasDefault = !ReferenceEquals(e.NewValue, AdamantiumProperty.UnsetValue);
        definition.LayoutChanged?.Invoke(component, EventArgs.Empty);
    }

    private void OnChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // A child is a definition like any other, so it JOINS the tree too - otherwise the three lines of a vector
        // would be the only ones in the inspector whose bindings resolve against nothing.
        foreach (var child in e.OldItems?.OfType<PropertyDefinition>() ?? Enumerable.Empty<PropertyDefinition>())
            RemoveLogicalChild(child);

        foreach (var child in e.NewItems?.OfType<PropertyDefinition>() ?? Enumerable.Empty<PropertyDefinition>())
            AddLogicalChild(child);

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

}

/// <summary>Text. The plain case, and the one everything else is measured against.</summary>
public class StringProperty : PropertyDefinition
{
    private DataTemplate _editor;

    protected internal override DataTemplate DefaultEditorTemplate =>
        _editor ??= new DataTemplate(() => new TemplateResult { RootComponent = PropertyEditors.Field() });

    protected internal override void PrepareEditor(IUIComponent editor, object value)
    {
        if (editor is TextBox box) box.Text = value?.ToString() ?? String.Empty;
    }

    protected internal override object ReadEditor(IUIComponent editor) => (editor as TextBox)?.Text;
}

/// <summary>A number, with the bounds and the step the property actually has - which is why they live here and not in
/// the template: a rotation is 0..360 and a scale is not.</summary>
public class NumericProperty : PropertyDefinition
{
    public static readonly AdamantiumProperty MinimumProperty = AdamantiumProperty.Register(nameof(Minimum),
        typeof(Double), typeof(NumericProperty), new PropertyMetadata(Double.MinValue));

    public static readonly AdamantiumProperty MaximumProperty = AdamantiumProperty.Register(nameof(Maximum),
        typeof(Double), typeof(NumericProperty), new PropertyMetadata(Double.MaxValue));

    public static readonly AdamantiumProperty StepProperty = AdamantiumProperty.Register(nameof(Step),
        typeof(Double), typeof(NumericProperty), new PropertyMetadata(1.0));

    public static readonly AdamantiumProperty DecimalsProperty = AdamantiumProperty.Register(nameof(Decimals),
        typeof(Int32), typeof(NumericProperty), new PropertyMetadata(3));

    /// <summary>Whether the line ends with the up and down buttons. ON by default, because a number a person nudges is
    /// what a stepper is for - but they cost width, and in a narrow inspector that width comes out of the field the
    /// number is actually read in. A panel that is short of room turns them off and keeps the digits.</summary>
    public static readonly AdamantiumProperty ShowButtonsProperty = AdamantiumProperty.Register(nameof(ShowButtons),
        typeof(Boolean), typeof(NumericProperty), new PropertyMetadata(true));

    private DataTemplate _editor;

    public Double Minimum
    {
        get => GetValue<Double>(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public Double Maximum
    {
        get => GetValue<Double>(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Double Step
    {
        get => GetValue<Double>(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>How many decimals the editor shows. Not how many the VALUE has - the property keeps its own precision.</summary>
    public Int32 Decimals
    {
        get => GetValue<Int32>(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    public Boolean ShowButtons
    {
        get => GetValue<Boolean>(ShowButtonsProperty);
        set => SetValue(ShowButtonsProperty, value);
    }

    protected internal override DataTemplate DefaultEditorTemplate =>
        _editor ??= new DataTemplate(() => new TemplateResult { RootComponent = PropertyEditors.Number() });

    protected internal override void PrepareEditor(IUIComponent editor, object value)
    {
        if (editor is not NumericUpDown numeric) return;

        numeric.Minimum = Minimum;
        numeric.Maximum = Maximum;
        numeric.SmallChange = Step;
        numeric.AreButtonsVisible = ShowButtons;
        // NULL, not zero, when there is nothing to show - the objects disagree. A zero here would be a number neither
        // of them holds, which is worse than an empty field: it reads as an answer.
        numeric.Value = value is IConvertible convertible
            ? Convert.ToDouble(convertible, System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }

    protected internal override object ReadEditor(IUIComponent editor) => (editor as NumericUpDown)?.Value;
}

/// <summary>A flag: a live box, flipped by one click like every other check box on the page.</summary>
public class BooleanProperty : PropertyDefinition
{
    private DataTemplate _display;
    private DataTemplate _editor;

    protected internal override DataTemplate DefaultValueTemplate => _display ??= PropertyEditors.Box(live: false);

    protected internal override DataTemplate DefaultEditorTemplate => _editor ??= PropertyEditors.Box(live: true);

    protected internal override void PrepareEditor(IUIComponent editor, object value)
    {
        if (editor is not ToggleButton toggle) return;

        // INDETERMINATE when the objects disagree: an unticked box would say they are all off, and half of them are on.
        // Three-state only while that is the case, so an ordinary row is an ordinary two-state box.
        toggle.IsThreeState = value is not bool;
        toggle.IsChecked = value as bool?;
    }

    protected internal override object ReadEditor(IUIComponent editor) => (editor as ToggleButton)?.IsChecked;
}

/// <summary>A choice from a list: an enum, or whatever the application offers. An enum fills the list by itself.</summary>
public class ChoiceProperty : PropertyDefinition
{
    public static readonly AdamantiumProperty ItemsSourceProperty = AdamantiumProperty.Register(nameof(ItemsSource),
        typeof(IEnumerable), typeof(ChoiceProperty), new PropertyMetadata(null));

    public static readonly AdamantiumProperty EnumTypeProperty = AdamantiumProperty.Register(nameof(EnumType),
        typeof(Type), typeof(ChoiceProperty), new PropertyMetadata(null));

    private DataTemplate _editor;

    public IEnumerable ItemsSource
    {
        get => GetValue<IEnumerable>(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Fills the list from an enum's values, so an enum property needs no list of its own.</summary>
    public Type EnumType
    {
        get => GetValue<Type>(EnumTypeProperty);
        set => SetValue(EnumTypeProperty, value);
    }

    protected internal override DataTemplate DefaultEditorTemplate =>
        _editor ??= new DataTemplate(() => new TemplateResult { RootComponent = PropertyEditors.Choice() });

    /// <summary>The choices for this property: the explicit list, else the enum's values, else nothing.</summary>
    public IEnumerable Choices()
    {
        if (ItemsSource != null) return ItemsSource;
        return EnumType is { IsEnum: true } type ? Enum.GetValues(type) : null;
    }

    protected internal override void PrepareEditor(IUIComponent editor, object value)
    {
        if (editor is not DropDown drop) return;

        drop.ItemsSource = Choices();
        drop.SelectedItem = value;
    }

    protected internal override object ReadEditor(IUIComponent editor) => (editor as DropDown)?.SelectedItem;
}

/// <summary>A <see cref="Color"/>, edited by the swatch button that opens the full picker. A colour is the one value
/// nobody can type: "#3A6EA5" says nothing to the eye and three numbers say less, so the editor has to SHOW it.</summary>
public class ColorProperty : PropertyDefinition
{
    private DataTemplate _editor;

    protected internal override DataTemplate DefaultEditorTemplate =>
        _editor ??= new DataTemplate(() => new TemplateResult { RootComponent = PropertyEditors.Swatch() });

    protected internal override bool EditorCanShowNothing => false;

    protected internal override void PrepareEditor(IUIComponent editor, object value)
    {
        if (editor is ColorPickerButton swatch && value is Color colour) swatch.SelectedColor = colour;
    }

    protected internal override object ReadEditor(IUIComponent editor) => (editor as ColorPickerButton)?.SelectedColor;
}

/// <summary>A <see cref="SolidColorBrush"/>, edited by the same swatch a <see cref="ColorProperty"/> uses. A property
/// that carries ONE colour is a solid brush and nothing else - a gradient has no single colour to show or to set - so
/// this line does not offer a choice of brush; the day a property may hold any brush, that is a different line with a
/// chooser in it.
/// <para>The colour is painted INTO the brush the property already holds. A brush is usually shared - a theme colour
/// stands behind a dozen elements - and replacing it would leave every one of them on the old one. Where the brush
/// refuses to be painted, being frozen, a new one is put in its place instead: frozen means immutable, and the property
/// can still be pointed at something else.</para></summary>
public class SolidColorBrushProperty : PropertyDefinition
{
    private DataTemplate _editor;

    protected internal override DataTemplate DefaultEditorTemplate =>
        _editor ??= new DataTemplate(() => new TemplateResult { RootComponent = PropertyEditors.Swatch() });

    protected internal override bool EditorCanShowNothing => false;

    protected internal override void PrepareEditor(IUIComponent editor, object value)
    {
        if (editor is ColorPickerButton swatch && value is SolidColorBrush brush) swatch.SelectedColor = brush.Color;
    }

    protected internal override object ReadEditor(IUIComponent editor) => (editor as ColorPickerButton)?.SelectedColor;

    // What the line shows of a brush is its COLOUR, so that is what "the same" means here. Two objects each holding
    // their own brush of the same colour agree as far as this line is concerned, and a row reporting otherwise would be
    // reporting a difference nobody can see.
    protected internal override bool SameValue(object left, object right) =>
        left is SolidColorBrush first && right is SolidColorBrush second
            ? first.Color == second.Color
            : base.SameValue(left, right);

    protected internal override bool WriteInto(object current, object edited)
    {
        // NOT into a brush the object is only holding. A theme's brush is handed to everything that asks for that
        // colour, so writing into it recoloured the whole application from one node's title strip - and the two lines
        // of a node that both start at the accent looked like one line, because they were pointed at one object. An
        // edit there means "this object overrides the theme", which is a NEW brush on the object and nothing else
        // touched.
        if (current is not SolidColorBrush { IsFrozen: false, IsShared: false } brush ||
            edited is not Color colour)
        {
            return false;
        }

        brush.Color = colour;
        return true;
    }

    // Reached only when the brush would not take the colour - it is frozen, it belongs to the theme, or there was no
    // brush there at all. A new one then, because refusing here would be the line quietly doing nothing.
    protected internal override bool TryConvert(object edited, Type target, out object value)
    {
        if (edited is Color colour)
        {
            value = new SolidColorBrush(colour);
            return true;
        }

        return base.TryConvert(edited, target, out value);
    }
}

/// <summary>A property that holds other properties: a vector, a colour, a nested object. It has no value of its own -
/// it opens.</summary>
public class CompositeProperty : PropertyDefinition
{
    /// <summary>A short line shown beside the name while it is folded, so the value is readable without opening it:
    /// <c>(12, 0, 4)</c>. Optional.</summary>
    public static readonly AdamantiumProperty SummaryProperty = AdamantiumProperty.Register(nameof(Summary),
        typeof(String), typeof(CompositeProperty), new PropertyMetadata(null));

    public static readonly AdamantiumProperty IsExpandedProperty = AdamantiumProperty.Register(nameof(IsExpanded),
        typeof(bool), typeof(CompositeProperty), new PropertyMetadata(false));

    public String Summary
    {
        get => GetValue<String>(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>Whether the children are shown. Lives on the DEFINITION rather than on the row, so folding survives the
    /// row being rebuilt - and so a view-model can state it.</summary>
    public bool IsExpanded
    {
        get => GetValue<bool>(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }
}

/// <summary>A group of rows PER ELEMENT of a collection: the lines written inside it are repeated for every item, each
/// one pointed at that item rather than at what is selected.
/// <para>What an inspector has no other way of showing. Everything else here is one line about one property of one
/// object, so a list of things that each have properties of their own - the sockets of a node, the stops of a gradient,
/// the columns of a table - could only be written out by hand, which means writing out a number of lines nobody knows
/// in advance. The count is data; the lines have to be data too.</para>
/// <para>Its own <see cref="PropertyDefinition.Binding"/> is what it reads the collection through, against whatever the
/// section is pointed at; its <see cref="Children"/> are the lines to repeat. The child lines bind against the ITEM -
/// <c>{Binding Name}</c> on a socket is the socket's name - which is the whole trick, and it costs the grid nothing
/// but a different set of targets.</para></summary>
public class ItemsProperty : CompositeProperty
{
    public static readonly AdamantiumProperty ItemHeaderProperty = AdamantiumProperty.Register(nameof(ItemHeader),
        typeof(BindingBase), typeof(ItemsProperty), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ItemActionProperty = AdamantiumProperty.Register(nameof(ItemAction),
        typeof(ICommand), typeof(ItemsProperty), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ItemActionIconProperty = AdamantiumProperty.Register(
        nameof(ItemActionIcon), typeof(String), typeof(ItemsProperty), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ItemActionTipProperty = AdamantiumProperty.Register(
        nameof(ItemActionTip), typeof(String), typeof(ItemsProperty), new PropertyMetadata(null));

    /// <summary>What each item is CALLED, read off the item itself - the socket's own name over its two lines. Nothing
    /// numbers them instead, which is what a list whose items have no names of their own wants.</summary>
    public BindingBase ItemHeader
    {
        get => GetValue<BindingBase>(ItemHeaderProperty);
        set => SetValue(ItemHeaderProperty, value);
    }

    /// <summary>What the button beside each item's name does, given that item.
    /// <para>The reason a list needs one: the COUNT can only take things off the end. Asked to drop the second of three
    /// sockets, a count says nothing at all - it drops the third and leaves the second where it was.</para></summary>
    public ICommand ItemAction
    {
        get => GetValue<ICommand>(ItemActionProperty);
        set => SetValue(ItemActionProperty, value);
    }

    /// <summary>The picture on that button and the words it says on hover - see <see cref="PropertyDefinition.ActionIcon"/>.
    /// Stated on the LIST, because the lines the button sits on are made as the grid builds and there is nowhere else to
    /// say it.</summary>
    public String ItemActionIcon
    {
        get => GetValue<String>(ItemActionIconProperty);
        set => SetValue(ItemActionIconProperty, value);
    }

    public String ItemActionTip
    {
        get => GetValue<String>(ItemActionTipProperty);
        set => SetValue(ItemActionTipProperty, value);
    }
}

// The line that carries ONE item's name inside an ItemsProperty. Made by the grid as it builds, and holding nothing
// worth keeping between passes - the item's own lines are the ones that read and write anything.
internal sealed class ItemHeaderLine : PropertyDefinition
{
}

// Every editor in an inspector is the SAME HEIGHT, and that height is the row's: they stretch into it rather than
// standing at whatever their own content asks for. A field as tall as one line of text beside a spinner as tall as its
// buttons beside a list that collapses when it happens to be empty is not a form, it is a pile.
internal static class PropertyEditors
{
    public static TextBox Field() => new()
    {
        BorderThickness = new Thickness(0),
        Padding = new Thickness(4, 0, 4, 0),
        MinWidth = 0,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Stretch,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    // Press and drag sideways to run the value up and down - what a property panel in a 3D tool does, and the fastest
    // way there is to nudge a position or a mass. On by default HERE and off in a form elsewhere, because it takes the
    // press over: dragging inside the field no longer selects text (double-click still does).
    public static NumericUpDown Number() => new()
    {
        MinWidth = 0,
        MinHeight = 0,
        IsDragScrubEnabled = true,
        VerticalAlignment = VerticalAlignment.Stretch,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    public static DropDown Choice() => new()
    {
        MinWidth = 0,
        MinHeight = 0,
        BorderThickness = new Thickness(0),
        VerticalAlignment = VerticalAlignment.Stretch,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    // No border of its own: the swatch IS the control, and a frame around a colour changes how the colour reads.
    public static ColorPickerButton Swatch() => new()
    {
        MinWidth = 0,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Stretch,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    public static DataTemplate Box(bool live) => new(() =>
    {
        var check = new CheckBox
        {
            IsHitTestVisible = live,
            MinWidth = 0,
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        var result = new TemplateResult { RootComponent = check };

        // The READ-ONLY box follows the value by binding, because nothing fills it: a property that cannot be edited has
        // no editor to prepare. The live one is filled by the definition itself, and a binding there would fight the
        // write-back.
        if (!live) result.AddBinding(check, "IsChecked", new Core.Data.Binding());

        return result;
    });
}
