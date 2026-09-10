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

    private readonly List<BoundValue> _values = new();
    private Grid _layout;
    private IInputComponent _grip;
    private IInputComponent _expander;
    private IInputComponent _editor;
    private ContentPresenter _valueHost;
    private ContentPresenter _nameHost;
    private bool _pendingEditor;
    private bool _writing;
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

    /// <summary>The live editor in the value half, or null on a read-only row.</summary>
    public IInputComponent Editor => _editor;

    /// <summary>Points the row at a definition and its objects. Called on creation and on every rebind.</summary>
    internal void Attach(PropertyGrid owner, PropertyDefinition definition, IReadOnlyList<object> targets, double indent)
    {
        Owner = owner;
        Definition = definition;
        Targets = targets ?? Array.Empty<object>();
        Indent = indent;

        HasChildren = definition is CompositeProperty composite && composite.Children.Count > 0;
        IsExpanded = definition is CompositeProperty { IsExpanded: true };
        IsReadOnly = definition.IsReadOnly;

        Bind();
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

        foreach (var bound in _values)
        {
            if (!bound.Write(value)) return false;
        }

        Read();
        return true;
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
            return;
        }

        var first = _values[0].Value;
        for (var i = 1; i < _values.Count; i++)
        {
            if (Equals(_values[i].Value, first)) continue;

            Value = null;
            IsMixed = true;
            return;
        }

        Value = first;
        IsMixed = false;
    }

    private void OnBoundValueChanged(object sender, EventArgs e)
    {
        Read();
        ApplyContent();
    }

    private void Unhook()
    {
        if (_grip != null) _grip.MouseLeftButtonDown -= OnGripPressed;
        if (_expander != null) _expander.MouseLeftButtonDown -= OnExpanderPressed;
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
        _valueHost.Content = HasChildren ? (Definition as CompositeProperty)?.Summary : Value;

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
        }
        finally
        {
            _writing = false;
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

        _editor = null;
    }

    // The OUTERMOST editor, and by kind before by focusability: a spinner contains a text box, so a hunt for "the first
    // focusable thing" finds the box inside it and the row would then read text out of a control that deals in numbers.
    private static IInputComponent FindEditor(IUIComponent root)
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is NumericUpDown or DropDown or ToggleButton or TextBox) return (IInputComponent)child;
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
        if (e.Property == ToggleButton.IsCheckedProperty) Commit();
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
