using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>One line of an inspector, drawn from a <see cref="PropertyDefinition"/>: name, the grip between the halves,
/// and the value. The value half holds a LIVE EDITOR, not a label that turns into one - an inspector is a form. A
/// read-only row shows text instead, which is also how it says it is read-only.
/// <para>The value comes from the definition's binding, one live <see cref="BoundValue"/> per inspected object, so the
/// row follows the model and writes back through the same binding.</para>
/// <para>Template: PART_Layout (a three-column Grid the row sets the widths of), PART_Name, PART_Grip, PART_Value, and
/// PART_Expander on a composite row.</para></summary>
public class PropertyRow : Control
{
    public static readonly AdamantiumProperty IsReadOnlyProperty = AdamantiumProperty.Register(nameof(IsReadOnly),
        typeof(bool), typeof(PropertyRow),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty IndentProperty = AdamantiumProperty.Register(nameof(Indent),
        typeof(Double), typeof(PropertyRow),
        new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty HasChildrenProperty = AdamantiumProperty.Register(nameof(HasChildren),
        typeof(bool), typeof(PropertyRow), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty IsExpandedProperty = AdamantiumProperty.Register(nameof(IsExpanded),
        typeof(bool), typeof(PropertyRow), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty ValueProperty = AdamantiumProperty.Register(nameof(Value),
        typeof(object), typeof(PropertyRow), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>The selected objects DISAGREE about this property. Shown as such rather than as an empty value - a
    /// blank field reads as "nothing", and the next keystroke would set four objects to what the user thought was one.</summary>
    public static readonly AdamantiumProperty IsMixedProperty = AdamantiumProperty.Register(nameof(IsMixed),
        typeof(bool), typeof(PropertyRow), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty IsModifiedProperty = AdamantiumProperty.Register(nameof(IsModified),
        typeof(bool), typeof(PropertyRow), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    /// <summary>Whether this row ends with the "..." button. Mirrored off the definition, like the rest of what the
    /// template triggers on - a template binds to the ROW, and the definition is not in its way.</summary>
    public static readonly AdamantiumProperty ShowActionButtonProperty = AdamantiumProperty.Register(
        nameof(ShowActionButton), typeof(bool), typeof(PropertyRow),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    private readonly List<BoundValue> _values = new();
    private Grid _layout;
    private IInputComponent _grip;
    private IInputComponent _expander;
    private ButtonBase _action;
    private ButtonBase _reset;
    private IInputComponent _editor;
    private ContentPresenter _valueHost;
    private ContentPresenter _nameHost;
    private bool _pendingEditor;
    private bool _writing;
    private bool _pushing;
    private bool _draggingGrip;
    private double _gripFrom;
    private double _widthFrom;

    /// <summary>What this row draws.</summary>
    public PropertyDefinition Definition { get; internal set; }

    /// <summary>The grid it belongs to - the source of the shared name width and of the writes.</summary>
    public PropertyGrid Owner { get; internal set; }

    /// <summary>The objects the row reads and writes - one of them in the ordinary case, several while a multiple
    /// selection is being inspected.</summary>
    public IReadOnlyList<object> Targets { get; private set; } = Array.Empty<object>();

    /// <summary>The first of <see cref="Targets"/> - what a row of a single-object inspector is about.</summary>
    public object Target => Targets.Count > 0 ? Targets[0] : null;

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>How far the NAME is pushed right - depth times the grid's indent. Only the name: the value half of a
    /// nested row still lines up with every other value, which is the whole point of a shared grip.</summary>
    public Double Indent
    {
        get => GetValue<Double>(IndentProperty);
        set => SetValue(IndentProperty, value);
    }

    public bool HasChildren
    {
        get => GetValue<bool>(HasChildrenProperty);
        set => SetValue(HasChildrenProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue<bool>(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>What the row currently holds.</summary>
    public object Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsMixed
    {
        get => GetValue<bool>(IsMixedProperty);
        set => SetValue(IsMixedProperty, value);
    }

    /// <summary>The type the property holds, read off the objects rather than off <see cref="Value"/> - which is null
    /// exactly when the objects disagree, and that is the case where a typed value still has to be converted before it
    /// can be written to all of them.</summary>
    public Type ValueType
    {
        get
        {
            foreach (var bound in _values)
            {
                if (bound.Value != null) return bound.Value.GetType();
            }

            return null;
        }
    }

    public bool ShowActionButton
    {
        get => GetValue<bool>(ShowActionButtonProperty);
        set => SetValue(ShowActionButtonProperty, value);
    }

    /// <summary>Whether what the objects hold is anything other than the property's default. The theme reads it to show
    /// the button that puts the default back - and the mark itself is worth having: an inspector of forty rows says at
    /// a glance which four were touched.</summary>
    public bool IsModified
    {
        get => GetValue<bool>(IsModifiedProperty);
        set => SetValue(IsModifiedProperty, value);
    }

    /// <summary>The live editor in the value half, or null on a read-only row.</summary>
    public IInputComponent Editor => _editor;

    /// <summary>Points the row at a definition and its objects. Called on creation and on every rebind.</summary>
    internal void Attach(PropertyGrid owner, PropertyDefinition definition, IReadOnlyList<object> targets, double indent)
    {
        // Same property, same objects - a REFRESH, not a rebind. Building the bindings again would cost the selection's
        // size twice over: every live value has to be let go one at a time, and letting one go is a search through the
        // rest. Measured on 50 000 objects, one write spent nearly five minutes there and nothing about what the row is
        // pointed at had changed.
        var rebind = !ReferenceEquals(Definition, definition) || !ReferenceEquals(Targets, targets);

        Owner = owner;
        Definition = definition;
        Targets = targets ?? Array.Empty<object>();
        Indent = indent;

        HasChildren = definition is CompositeProperty composite && composite.Children.Count > 0;
        IsExpanded = definition is CompositeProperty { IsExpanded: true };
        IsReadOnly = definition.IsReadOnly;
        ShowActionButton = definition.ShowActionButton;

        if (rebind) Bind();
        else Read();

        ApplyContent();
    }

    /// <summary>Writes what the editor holds. What the editors' own signals call, and what a test can call directly.</summary>
    public bool Commit()
    {
        if (_writing || Definition == null || IsReadOnly || _editor == null) return false;

        return Owner?.Write(this, Definition.ReadEditor(_editor)) ?? false;
    }

    /// <summary>Pushes one value into every object the row stands for, through their bindings.</summary>
    internal bool WriteValue(object value)
    {
        if (_values.Count == 0) return false;

        // Every object's write raises the very signal the row listens to, and answering each one re-reads ALL of them:
        // one value pushed to a selection of N costs N reads of N values. Measured on a selection of 50 000, a single
        // write took five minutes. The row already knows what it is writing, so their signals say nothing it does not
        // know - held off, and the row reads itself ONCE when the push is over.
        var landed = true;
        _pushing = true;
        try
        {
            foreach (var bound in _values)
            {
                if (bound.Write(value)) continue;

                landed = false;
                break;
            }
        }
        finally
        {
            _pushing = false;
        }

        Read();
        ApplyContent();
        return landed;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Unhook();

        _layout = GetTemplateChild("PART_Layout") as Grid;
        _grip = GetTemplateChild("PART_Grip") as IInputComponent;
        _expander = GetTemplateChild("PART_Expander") as IInputComponent;
        _valueHost = GetTemplateChild("PART_Value") as ContentPresenter;
        _nameHost = GetTemplateChild("PART_Name") as ContentPresenter;

        if (_grip != null)
        {
            _grip.MouseLeftButtonDown += OnGripPressed;
            if (_grip is UIComponent grip) grip.Cursor = Cursors.SizeEWE;
        }

        if (_expander != null) _expander.MouseLeftButtonDown += OnExpanderPressed;

        _action = GetTemplateChild("PART_Action") as ButtonBase;
        if (_action != null) _action.Click += OnActionPressed;

        _reset = GetTemplateChild("PART_Reset") as ButtonBase;
        if (_reset != null) _reset.Click += OnResetPressed;

        ApplyContent();
        ApplyNameWidth();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        Unhook();

        _layout = null;
        _grip = null;
        _expander = null;
        _valueHost = null;
        _nameHost = null;
    }

    /// <summary>Puts the shared name width into the layout. Called by the grid whenever the grip moves - one number for
    /// every row, or the columns of an inspector turn into a staircase.</summary>
    internal void ApplyNameWidth()
    {
        if (_layout == null || Owner == null || _layout.ColumnDefinitions.Count < 3) return;

        _layout.ColumnDefinitions[0].Width = new GridLength(Math.Max(0, Owner.NameColumnWidth));

        // A column definition changing its width dirties NOTHING by itself, so the path is dirtied by hand - starting
        // at the LAYOUT, not at the row: one valid element on the way stops the pass before it reaches the grid.
        for (IUIComponent node = _layout; node != null; node = node.VisualParent)
        {
            (node as IMeasurableComponent)?.InvalidateMeasure();
            if (ReferenceEquals(node, Owner)) break;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // The editor is built by the presenter during measure, so it can only be found once a pass has run.
        if (_pendingEditor) HookEditor();
        return size;
    }

    // One live binding per inspected object. They are logical children so a value binding that reads an ancestor
    // instead of the object - which the author is free to write - resolves against the tree this row stands in.
    private void Bind()
    {
        foreach (var bound in _values)
        {
            bound.Changed -= OnBoundValueChanged;
            bound.Release();
            RemoveLogicalChild(bound);
        }

        _values.Clear();

        if (Definition?.Binding == null) return;

        foreach (var target in Targets)
        {
            var bound = new BoundValue();
            AddLogicalChild(bound);
            bound.PointAt(target, Definition.Binding);
            bound.Changed += OnBoundValueChanged;
            _values.Add(bound);
        }

        Read();
    }

    // The common value, or nothing at all when the objects disagree.
    private void Read()
    {
        if (_values.Count == 0)
        {
            Value = null;
            IsMixed = false;
            IsModified = false;
            return;
        }

        var first = _values[0].Value;
        for (var i = 1; i < _values.Count; i++)
        {
            // The DEFINITION says what "the same" means: two brushes of one colour are two instances, and comparing
            // them here would make the row report a difference nobody can see.
            if (Definition?.SameValue(_values[i].Value, first) ?? Equals(_values[i].Value, first)) continue;

            Value = null;
            IsMixed = true;

            // Objects that disagree cannot ALL be at the default - at most one of them is. So the row is modified, and
            // resetting it is the one edit that makes them agree again.
            IsModified = !IsReadOnly && Definition?.HasDefault == true;
            return;
        }

        Value = first;
        IsMixed = false;

        // Not on a read-only row: the reset it would offer is a write, and a write there is refused. A mark promising
        // a button that does nothing is worse than no mark.
        IsModified = !IsReadOnly && Definition is { HasDefault: true } && !Definition.SameValue(first, Default());
    }

    // The default AS THE PROPERTY WOULD HOLD IT. Written in markup it arrives as text - `DefaultValue="80"` is the
    // string "80", not the number - and comparing that with what the object holds would mark every such row as edited
    // the moment it was shown. The same conversion a write goes through, so the two agree about what the value is.
    private object Default()
    {
        var wanted = Definition?.DefaultValue;

        return Definition != null && Definition.TryConvert(wanted, ValueType, out var value) ? value : wanted;
    }

    private void OnBoundValueChanged(object sender, EventArgs e)
    {
        if (_pushing) return;

        Read();
        ApplyContent();
    }

    private void Unhook()
    {
        if (_grip != null) _grip.MouseLeftButtonDown -= OnGripPressed;
        if (_expander != null) _expander.MouseLeftButtonDown -= OnExpanderPressed;
        if (_action != null) _action.Click -= OnActionPressed;
        if (_reset != null) _reset.Click -= OnResetPressed;
        UnhookEditor();
    }

    private void ApplyContent()
    {
        if (Definition == null) return;

        if (_nameHost != null)
        {
            _nameHost.Content = Definition.Header;
            _nameHost.ContentTemplate = null;
        }

        if (_expander is MeasurableUIComponent strip) strip.Margin = new Thickness(Math.Max(0, Indent), 0, 0, 0);
        if (_valueHost == null) return;

        // A composite row has no value of its own; a read-only one shows text. Everything else gets its editor, and
        // keeps it - the editor is the row, not a state of it.
        var template = HasChildren
            ? null
            : IsReadOnly ? Definition.ValueFor() : Definition.EditorFor() ?? Definition.ValueFor();
        var rebuilt = !ReferenceEquals(_valueHost.ContentTemplate, template);

        if (rebuilt) UnhookEditor();

        _valueHost.ContentTemplate = template;

        // A presenter given null content builds NOTHING, editor included - and the row's value is null exactly when the
        // selected objects disagree. Left at null the row would lose its editor at the one moment it is most needed:
        // putting ONE value on all of them is what inspecting several objects is for. Empty content instead, so the
        // editor is built and stands empty - unless the definition says its editor has no empty state, and a blank row
        // is then the honest answer rather than an editor showing a value neither object holds.
        _valueHost.Content = HasChildren
            ? (Definition as CompositeProperty)?.Summary
            : Value ?? (template != null && Definition.EditorCanShowNothing ? string.Empty : null);

        if (rebuilt || _editor == null) _pendingEditor = template != null && !IsReadOnly;
        else Fill();
    }

    private void Fill()
    {
        if (_editor == null || Definition == null) return;

        // Guarded, because filling the editor raises the very signals a user's change raises - and a write started from
        // a refresh would push the value the row has just read back into the model.
        _writing = true;
        try
        {
            Definition.PrepareEditor(_editor, Value);
            MarkMixed();
        }
        finally
        {
            _writing = false;
        }
    }

    // An empty editor says nothing on its own, and "nothing" is not what happened - the objects disagree, and for a
    // number it is not even a state the property can be in. So the editor's own prompt says which it is, and it goes
    // the moment they agree. ONLY then: a row whose objects hold one value shows that value like any other row.
    private void MarkMixed()
    {
        var prompt = IsMixed ? Owner?.MixedText : null;

        switch (_editor)
        {
            case NumericUpDown numeric: numeric.Placeholder = prompt; break;
            case TextBoxBase box: box.Placeholder = prompt; break;
            case DropDown drop: drop.Placeholder = prompt; break;
        }
    }

    private void HookEditor()
    {
        _pendingEditor = false;
        _editor = FindEditor(this);
        if (_editor == null) return;

        // One height for the whole inspector, stated rather than inherited: a control left to itself stands at whatever
        // its own content asks for, and three kinds of editor then stand at three different heights. A check box keeps
        // its square - it is a glyph, not a field.
        if (_editor is not ToggleButton && _editor is MeasurableUIComponent sized && Owner is { EditorHeight: > 0 })
            sized.Height = Owner.EditorHeight;

        Fill();

        // Each kind of editor says "the user changed me" its own way, and these are the four the inspector ships.
        if (_editor is TextBox box)
        {
            box.EnterPressed += OnEditorEntered;
            box.LostFocus += OnEditorLostFocus;
        }

        if (_editor is NumericUpDown numeric) numeric.ValueChanged += OnEditorValueChanged;
        if (_editor is DropDown drop) drop.SelectionChanged += OnEditorChosen;
        if (_editor is ToggleButton toggle) toggle.PropertyChanged += OnTogglePropertyChanged;
        if (_editor is ColorPickerButton swatch) swatch.PropertyChanged += OnSwatchPropertyChanged;
    }

    private void UnhookEditor()
    {
        if (_editor == null) return;

        if (_editor is TextBox box)
        {
            box.EnterPressed -= OnEditorEntered;
            box.LostFocus -= OnEditorLostFocus;
        }

        if (_editor is NumericUpDown numeric) numeric.ValueChanged -= OnEditorValueChanged;
        if (_editor is DropDown drop) drop.SelectionChanged -= OnEditorChosen;
        if (_editor is ToggleButton toggle) toggle.PropertyChanged -= OnTogglePropertyChanged;
        if (_editor is ColorPickerButton swatch) swatch.PropertyChanged -= OnSwatchPropertyChanged;

        _editor = null;
    }

    // The OUTERMOST editor, and by kind before by focusability: a spinner contains a text box, so a hunt for "the first
    // focusable thing" finds the box inside it and the row would then read text out of a control that deals in numbers.
    private static IInputComponent FindEditor(IUIComponent root)
    {
        foreach (var child in root.VisualChildren)
        {
            // The swatch is named here for the same reason the spinner is: it CONTAINS a picker full of fields and
            // sliders, and a hunt for "the first focusable thing" would come back with one of those.
            if (child is NumericUpDown or DropDown or ToggleButton or TextBox or ColorPickerButton)
                return (IInputComponent)child;
            if (FindEditor(child) is { } nested) return nested;
        }

        return Focusable(root);
    }

    private static IInputComponent Focusable(IUIComponent root)
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is IInputComponent { Focusable: true } editor) return editor;
            if (Focusable(child) is { } nested) return nested;
        }

        return null;
    }

    private void OnEditorEntered(object sender, KeyEventArgs e)
    {
        if (Commit()) e.Handled = true;
    }

    private void OnEditorLostFocus(object sender, RoutedEventArgs e) => Commit();

    private void OnEditorValueChanged(object sender, EventArgs e) => Commit();

    private void OnEditorChosen(object sender, EventArgs e) => Commit();

    private void OnTogglePropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != ToggleButton.IsCheckedProperty) return;

        // A click on an indeterminate box lands on FALSE - that is the three-state cycle - while the row's rule for a
        // boolean the objects disagree on is TRUE, because leaving them disagreeing is the one thing nobody clicked
        // for. One rule whichever way the row is flipped.
        if (IsMixed && Owner != null) Owner.ToggleRow(this);
        else Commit();
    }

    // A colour is chosen by DRAGGING inside the picker, so this fires all the way through the gesture rather than once
    // at the end. That is wanted: the object being inspected follows the pointer, which is the whole reason a colour is
    // picked visually instead of typed.
    private void OnSwatchPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == ColorPickerButton.SelectedColorProperty) Commit();
    }

    /// <summary>Runs the definition's action. The row hands over the OBJECTS it stands for unless the definition named
    /// a parameter of its own: a command that opens a longer form for this property needs to know what it is being
    /// opened on, and having to say so on every line is a thing to forget.</summary>
    /// <summary>Puts the property's default back on every object the row stands for. Goes through the same write as an
    /// edit does, so a definition that paints INTO its value still paints instead of replacing, and a read-only row
    /// refuses exactly as it would refuse anything else.</summary>
    public bool ResetToDefault()
    {
        if (Definition is not { HasDefault: true } || Owner == null) return false;

        return Owner.Write(this, Definition.DefaultValue);
    }

    public bool RunAction()
    {
        if (Definition?.ActionCommand is not { } command) return false;

        var parameter = Definition.ActionCommandParameter ?? (Targets.Count == 1 ? Targets[0] : Targets);
        if (!command.CanExecute(parameter)) return false;

        command.Execute(parameter);
        return true;
    }

    private void OnActionPressed(object sender, RoutedEventArgs e)
    {
        if (RunAction()) e.Handled = true;
    }

    private void OnResetPressed(object sender, RoutedEventArgs e)
    {
        if (ResetToDefault()) e.Handled = true;
    }

    private void OnExpanderPressed(object sender, MouseButtonEventArgs e)
    {
        if (!HasChildren) return;

        Owner?.ToggleComposite(this);
        e.Handled = true;
    }

    private void OnGripPressed(object sender, MouseButtonEventArgs e)
    {
        if (Owner == null) return;

        _draggingGrip = true;
        _gripFrom = e.GetPosition(this).X;
        _widthFrom = Owner.NameColumnWidth;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (!_draggingGrip || Owner == null) return;

        Owner.NameColumnWidth = _widthFrom + (e.GetPosition(this).X - _gripFrom);
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        if (!_draggingGrip) return;

        _draggingGrip = false;
        ReleaseMouseCapture();
    }
}
