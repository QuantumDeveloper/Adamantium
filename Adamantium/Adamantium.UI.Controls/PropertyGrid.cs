using System.Collections;
using System.Collections.Specialized;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>What was written, and to what. A null <see cref="Property"/> means every property of that object.</summary>
public class PropertyValuesChangedEventArgs : EventArgs
{
    public PropertyValuesChangedEventArgs(object target = null, PropertyDefinition property = null)
    {
        Target = target;
        Property = property;
    }

    public object Target { get; }

    public PropertyDefinition Property { get; }
}

/// <summary>An inspector: names on the left, values on the right, one draggable grip between them, rows grouped into
/// folding sections. The TYPE belongs to the PROPERTY, not to a column, which is what makes this a different control
/// from <see cref="TreeDataGrid"/> rather than a mode of it.
/// <para>What is declared are <see cref="PropertyDefinition"/>s; the rows drawn from them are
/// <see cref="PropertyRow"/>s, made and remade by the grid. One definition serves every selected object at once.</para></summary>
public class PropertyGrid : Control
{
    public static readonly AdamantiumProperty SelectedObjectProperty = AdamantiumProperty.Register(
        nameof(SelectedObject), typeof(object), typeof(PropertyGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnSelectedObjectChanged));

    /// <summary>The objects being inspected together. An editor almost always has several things selected, and one
    /// definition then stands for the same property on all of them: it shows their common value, says so when they
    /// differ, and writes to every one of them.</summary>
    public static readonly AdamantiumProperty SelectedObjectsProperty = AdamantiumProperty.Register(
        nameof(SelectedObjects), typeof(IEnumerable), typeof(PropertyGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnSelectedObjectChanged));

    /// <summary>Width of the NAME half. One number for the whole inspector - a per-section width would let the columns
    /// of two sections drift apart, and the thing stops reading as a table.</summary>
    public static readonly AdamantiumProperty NameColumnWidthProperty = AdamantiumProperty.Register(
        nameof(NameColumnWidth), typeof(Double), typeof(PropertyGrid),
        new PropertyMetadata(140.0, PropertyMetadataOptions.AffectsMeasure, OnNameColumnWidthChanged));

    public static readonly AdamantiumProperty MinNameColumnWidthProperty = AdamantiumProperty.Register(
        nameof(MinNameColumnWidth), typeof(Double), typeof(PropertyGrid), new PropertyMetadata(60.0));

    public static readonly AdamantiumProperty MinValueColumnWidthProperty = AdamantiumProperty.Register(
        nameof(MinValueColumnWidth), typeof(Double), typeof(PropertyGrid), new PropertyMetadata(60.0));

    public static readonly AdamantiumProperty IndentProperty = AdamantiumProperty.Register(nameof(Indent),
        typeof(Double), typeof(PropertyGrid), new PropertyMetadata(14.0, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>How tall every editor in the inspector stands. ONE number, set by the theme: left to themselves a field
    /// is as tall as a line of text, a spinner as tall as its buttons, and a list collapses when nobody filled it - and
    /// the form reads as a pile. A check box is the exception and keeps its square.</summary>
    public static readonly AdamantiumProperty EditorHeightProperty = AdamantiumProperty.Register(nameof(EditorHeight),
        typeof(Double), typeof(PropertyGrid), new PropertyMetadata(20.0, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Sections from somewhere ELSE - what <see cref="PropertyDefinitionBuilder"/> made from a type, what an
    /// editor assembled per component. Set, it replaces <see cref="Sections"/> entirely: an inspector is either written
    /// out or generated, and mixing the two silently would be a puzzle for whoever reads the markup.</summary>
    public static readonly AdamantiumProperty SectionsSourceProperty = AdamantiumProperty.Register(
        nameof(SectionsSource), typeof(IEnumerable), typeof(PropertyGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnSelectedObjectChanged));

    private readonly List<PropertyRow> _rows = new();
    private Panel _host;
    private PropertyRow _selected;

    static PropertyGrid()
    {
        FocusableProperty.OverrideMetadata(typeof(PropertyGrid), new PropertyMetadata(true));
    }

    public PropertyGrid()
    {
        Sections.CollectionChanged += OnSectionsChanged;
    }

    /// <summary>The sections, in display order. [Content], so an inspector is written as the list of sections it is.</summary>
    [Content]
    public PropertySections Sections { get; } = new();

    public IEnumerable SectionsSource
    {
        get => GetValue<IEnumerable>(SectionsSourceProperty);
        set => SetValue(SectionsSourceProperty, value);
    }

    /// <summary>The object being inspected. A section may name its own <see cref="PropertySection.Target"/>; the rest
    /// read this one.</summary>
    public object SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    public IEnumerable SelectedObjects
    {
        get => GetValue<IEnumerable>(SelectedObjectsProperty);
        set => SetValue(SelectedObjectsProperty, value);
    }

    /// <summary>Everything a row stands for right now: the several, or the one, or nothing.</summary>
    public IReadOnlyList<object> Targets
    {
        get
        {
            if (SelectedObjects == null)
                return SelectedObject == null ? Array.Empty<object>() : new[] { SelectedObject };

            var many = new List<object>();
            foreach (var item in SelectedObjects)
            {
                if (item != null) many.Add(item);
            }

            return many;
        }
    }

    public Double NameColumnWidth
    {
        get => GetValue<Double>(NameColumnWidthProperty);
        set => SetValue(NameColumnWidthProperty, value);
    }

    public Double MinNameColumnWidth
    {
        get => GetValue<Double>(MinNameColumnWidthProperty);
        set => SetValue(MinNameColumnWidthProperty, value);
    }

    public Double MinValueColumnWidth
    {
        get => GetValue<Double>(MinValueColumnWidthProperty);
        set => SetValue(MinValueColumnWidthProperty, value);
    }

    /// <summary>Pixels of indent per level of nesting, applied to the NAME half only.</summary>
    public Double Indent
    {
        get => GetValue<Double>(IndentProperty);
        set => SetValue(IndentProperty, value);
    }

    public Double EditorHeight
    {
        get => GetValue<Double>(EditorHeightProperty);
        set => SetValue(EditorHeightProperty, value);
    }

    /// <summary>The row the pointer or the keyboard is on.</summary>
    public PropertyRow SelectedRow => _selected;

    /// <summary>Raised after a value has been written.</summary>
    public event EventHandler<PropertyValuesChangedEventArgs> ValueChanged;

    /// <summary>Rebuilds every section's rows - after the sections change, the object changes, or a composite folds.</summary>
    public void Rebuild()
    {
        if (_host == null) return;

        _host.Children.Clear();
        _rows.Clear();

        foreach (var section in DisplayedSections())
        {
            var rows = new StackPanel { Orientation = Orientation.Vertical };

            // A section may inspect something of its own; the rest follow the selection - all of it.
            var targets = section.Target != null ? new[] { section.Target } : Targets;

            foreach (var definition in section.Properties) AddRow(rows, definition, targets, 0);

            section.Content = rows;
            _host.Children.Add(section);
        }
    }

    /// <summary>What a definition holds on ONE object, read through its binding. For a question asked once - a test, a
    /// report; a row on screen keeps its bindings live instead.</summary>
    public object ValueOf(object target, PropertyDefinition definition)
    {
        if (target == null || definition?.Binding == null) return null;

        var probe = new BoundValue();
        probe.PointAt(target, definition.Binding);
        var value = probe.Value;
        probe.Release();

        return value;
    }

    /// <summary>What a definition holds across SEVERAL objects: their common value, or nothing at all when they differ -
    /// which <paramref name="mixed"/> is what says. A blank cell with no explanation would read as "empty", and the user
    /// would overwrite four objects thinking he was filling one in.</summary>
    public object ValueOf(IReadOnlyList<object> targets, PropertyDefinition definition, out bool mixed)
    {
        mixed = false;
        if (targets == null || targets.Count == 0 || definition == null) return null;

        var first = ValueOf(targets[0], definition);
        for (var i = 1; i < targets.Count; i++)
        {
            if (Equals(ValueOf(targets[i], definition), first)) continue;

            mixed = true;
            return null;
        }

        return first;
    }

    /// <summary>Writes what a row's editor produced to every object the row stands for. Called by the row when its
    /// editor says the user changed something - there is no edit MODE to enter or leave: the editors are live.</summary>
    public bool Write(PropertyRow row, object edited)
    {
        if (row?.Definition == null || row.IsReadOnly) return false;

        // Every selected object gets the SAME value - that is what editing a multiple selection means.
        if (!row.Definition.TryConvert(edited, row.Value?.GetType(), out var value)) return false;
        if (!row.WriteValue(value)) return false;

        ValueChanged?.Invoke(this, new PropertyValuesChangedEventArgs(row.Targets.Count > 0 ? row.Targets[0] : null,
            row.Definition));
        Refresh(null, row.Definition);
        return true;
    }

    /// <summary>Flips a boolean row. What the box in the row does, offered for a keyboard shortcut or a test.</summary>
    public bool ToggleRow(PropertyRow row)
    {
        if (row?.Definition == null || row.IsReadOnly) return false;

        // Mixed goes to TRUE, not to "the opposite of nothing": with three objects half on and half off, the one thing
        // the user cannot have meant is to leave them disagreeing.
        var current = !row.IsMixed && row.Value as bool? == true;
        return Write(row, !current);
    }

    /// <summary>Opens or folds a composite row, showing or hiding its children.</summary>
    public void ToggleComposite(PropertyRow row)
    {
        if (row?.Definition is not CompositeProperty composite) return;

        composite.IsExpanded = !composite.IsExpanded;
        Rebuild();
    }

    internal void SelectRow(PropertyRow row) => _selected = row;

    /// <summary>Re-reads one property, or every one when told nothing in particular.</summary>
    public void Refresh(object target = null, PropertyDefinition definition = null)
    {
        foreach (var row in _rows)
        {
            if (definition != null && !ReferenceEquals(row.Definition, definition)) continue;
            if (target != null && !row.Targets.Contains(target)) continue;

            row.Attach(this, row.Definition, row.Targets, row.Indent);
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _host = GetTemplateChild("PART_Sections") as Panel;
        Rebuild();
    }

    private IEnumerable<PropertySection> DisplayedSections()
    {
        if (SectionsSource == null) return Sections;

        var many = new List<PropertySection>();
        foreach (var item in SectionsSource)
        {
            if (item is PropertySection section) many.Add(section);
        }

        return many;
    }

    private void AddRow(Panel host, PropertyDefinition definition, IReadOnlyList<object> targets, int depth)
    {
        if (!definition.IsVisible) return;

        var row = new PropertyRow();
        row.Attach(this, definition, targets, depth * Indent);

        host.Children.Add(row);
        _rows.Add(row);

        if (definition is not CompositeProperty { IsExpanded: true } composite) return;

        foreach (var child in composite.Children) AddRow(host, child, targets, depth + 1);
    }

    private void OnSectionsChanged(object sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private static void OnSelectedObjectChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e) =>
        (d as PropertyGrid)?.Rebuild();

    private static void OnNameColumnWidthChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not PropertyGrid grid) return;

        var wanted = Math.Max(grid.MinNameColumnWidth, (double)e.NewValue);
        var room = grid.ActualWidth - grid.MinValueColumnWidth;
        if (room > grid.MinNameColumnWidth && wanted > room) wanted = room;

        if (Math.Abs(wanted - (double)e.NewValue) > 0.01)
        {
            grid.NameColumnWidth = wanted;
            return;
        }

        foreach (var row in grid._rows) row.ApplyNameWidth();
    }
}
