using System.Collections;
using System.Collections.Specialized;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
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

    /// <summary>What an editor says while the selected objects hold more than one value between them, shown as the
    /// prompt of an empty editor. Only then: a row they all agree on shows that value like any other row.
    /// <para>One phrase rather than the values themselves - a hundred objects selected is the ordinary case, and
    /// listing what each holds would fill the row with a line nobody can read.</para></summary>
    public static readonly AdamantiumProperty MixedTextProperty = AdamantiumProperty.Register(nameof(MixedText),
        typeof(String), typeof(PropertyGrid), new PropertyMetadata("multiple values"));

    /// <summary>Sections from somewhere ELSE - what <see cref="PropertyDefinitionBuilder"/> made from a type, what an
    /// editor assembled per component. Set, it replaces <see cref="Sections"/> entirely: an inspector is either written
    /// out or generated, and mixing the two silently would be a puzzle for whoever reads the markup.</summary>
    public static readonly AdamantiumProperty SectionsSourceProperty = AdamantiumProperty.Register(
        nameof(SectionsSource), typeof(IEnumerable), typeof(PropertyGrid),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnSelectedObjectChanged));

    /// <summary>Narrows the inspector to the properties whose name carries this, ignoring case. A section whose own
    /// name carries it keeps all of its properties - asking for "Transform" means the whole of it.
    /// <para>While it is set, a composite opens whether or not it was folded and so does a section holding a match:
    /// a search that hides its own results behind something folded is worse than no search. Both go back to how the
    /// user left them once it is cleared.</para></summary>
    public static readonly AdamantiumProperty SearchTextProperty = AdamantiumProperty.Register(nameof(SearchText),
        typeof(String), typeof(PropertyGrid), new PropertyMetadata(null, OnSearchTextChanged));

    /// <summary>Whether the inspector carries its search field. An inspector of five rows does not need one.
    /// <para>Taking the field away drops whatever was being searched for: an inspector left narrowed down with nothing
    /// on it to widen it again is a trap, and looks like an inspector that has lost most of its properties.</para>
    /// </summary>
    public static readonly AdamantiumProperty ShowSearchProperty = AdamantiumProperty.Register(nameof(ShowSearch),
        typeof(Boolean), typeof(PropertyGrid),
        new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure, OnShowSearchChanged));

    /// <summary>Whether a search is running. The control keeps it; the theme reads it to show the button that drops
    /// the search - a cross standing on an empty field would be offering to undo nothing.</summary>
    public static readonly AdamantiumProperty HasSearchTextProperty = AdamantiumProperty.Register(
        nameof(HasSearchText), typeof(Boolean), typeof(PropertyGrid), new PropertyMetadata(false));

    private readonly List<PropertyRow> _rows = new();
    private readonly List<PropertySection> _openedBySearch = new();
    private Panel _host;
    private TextBox _search;
    private ButtonBase _clearSearch;
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

    public String MixedText
    {
        get => GetValue<String>(MixedTextProperty);
        set => SetValue(MixedTextProperty, value);
    }

    public String SearchText
    {
        get => GetValue<String>(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public Boolean ShowSearch
    {
        get => GetValue<Boolean>(ShowSearchProperty);
        set => SetValue(ShowSearchProperty, value);
    }

    public Boolean HasSearchText
    {
        get => GetValue<Boolean>(HasSearchTextProperty);
        set => SetValue(HasSearchTextProperty, value);
    }

    /// <summary>Drops the search and shows every property again. What Escape does, and what the cross in the field
    /// does - one way out, whichever is reached for.</summary>
    public void ClearSearch()
    {
        if (String.IsNullOrEmpty(SearchText)) return;

        SearchText = null;
        _search?.Focus();
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

        var wanted = SearchText?.Trim();
        var searching = !String.IsNullOrEmpty(wanted);

        // The sections the search forced open are opened again from scratch every pass, so whatever is still in the
        // list is a section the user had folded himself before this search started - give it back.
        if (!searching) ReleaseOpened();

        foreach (var section in DisplayedSections())
        {
            // A section named for what is being looked for keeps all of its properties: asking for "Transform" means
            // the whole of it, not the one row that happens to repeat the word.
            var whole = searching && Carries(section.Header as String, wanted);
            var rows = new StackPanel { Orientation = Orientation.Vertical };

            // A section may inspect something of its own; the rest follow the selection - all of it.
            var targets = section.Target != null ? new[] { section.Target } : Targets;

            foreach (var definition in section.Properties)
            {
                AddRow(rows, definition, targets, 0, whole ? null : wanted);
            }

            // Assigned BEFORE the section may be dropped: a section left holding the rows of the last pass would keep
            // answering with them long after the search stopped agreeing.
            section.Content = rows;

            // A section with nothing in it is not an empty section, it is one the search has no answer from - and a
            // column of empty headers reads as a result.
            if (searching && rows.Children.Count == 0) continue;

            _host.Children.Add(section);

            // SetCurrentValue, not the setter: folding state is two-way bindable, and a Local value written from here
            // would outrank the binding and leave the section deaf to its own view-model ever after.
            if (searching && !section.IsExpanded)
            {
                _openedBySearch.Add(section);
                section.SetCurrentValue(Expander.IsExpandedProperty, true);
            }
        }
    }

    private void ReleaseOpened()
    {
        foreach (var section in _openedBySearch) section.SetCurrentValue(Expander.IsExpandedProperty, false);

        _openedBySearch.Clear();
    }

    private static bool Carries(String text, String wanted) =>
        text != null && text.Contains(wanted, StringComparison.CurrentCultureIgnoreCase);

    // Whether this definition, or anything under it, answers the search. A composite whose CHILD matches has to stand:
    // hiding the parent would hide the answer.
    private static bool Answers(PropertyDefinition definition, String wanted)
    {
        if (Carries(definition.Header as String, wanted)) return true;
        if (definition is not CompositeProperty composite) return false;

        foreach (var child in composite.Children)
        {
            if (Answers(child, wanted)) return true;
        }

        return false;
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

        // INTO the value first, where the definition says the value is an object with parts rather than a thing to be
        // replaced. Nothing is written through the binding then: the property still points at the same object, which is
        // the whole point - everything else holding it follows.
        if (row.Definition.WriteInto(row.Value, edited))
        {
            ValueChanged?.Invoke(this, new PropertyValuesChangedEventArgs(row.Targets.Count > 0 ? row.Targets[0] : null,
                row.Definition));
            Refresh(null, row.Definition);
            return true;
        }

        // Every selected object gets the SAME value - that is what editing a multiple selection means.
        if (!row.Definition.TryConvert(edited, row.ValueType, out var value)) return false;
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

        UnhookSearch();

        _host = GetTemplateChild("PART_Sections") as Panel;
        _search = GetTemplateChild("PART_Search") as TextBox;
        _clearSearch = GetTemplateChild("PART_ClearSearch") as ButtonBase;

        if (_search != null)
        {
            _search.Text = SearchText;
            _search.PropertyChanged += OnSearchTyped;
            _search.KeyDown += OnSearchKeyDown;
        }

        if (_clearSearch != null) _clearSearch.Click += OnClearSearchPressed;

        Rebuild();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        UnhookSearch();

        _search = null;
        _clearSearch = null;
        _host = null;
    }

    private void UnhookSearch()
    {
        if (_search != null)
        {
            _search.PropertyChanged -= OnSearchTyped;
            _search.KeyDown -= OnSearchKeyDown;
        }

        if (_clearSearch != null) _clearSearch.Click -= OnClearSearchPressed;
    }

    private void OnSearchTyped(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty) SearchText = _search?.Text;
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || String.IsNullOrEmpty(SearchText)) return;

        ClearSearch();
        e.Handled = true;
    }

    private void OnClearSearchPressed(object sender, RoutedEventArgs e) => ClearSearch();

    // Escape anywhere in the inspector, not only in the field: a search is narrowed down, then a row is clicked, and by
    // then the field no longer has the focus - and the way out has to be the same key it was a moment ago.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.Key != Key.Escape || String.IsNullOrEmpty(SearchText)) return;

        ClearSearch();
        e.Handled = true;
    }

    private static void OnShowSearchChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is PropertyGrid grid && e.NewValue is false) grid.ClearSearch();
    }

    private static void OnSearchTextChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not PropertyGrid grid) return;

        var wanted = e.NewValue as String;
        if (grid._search != null && grid._search.Text != wanted) grid._search.Text = wanted;

        grid.HasSearchText = !String.IsNullOrEmpty(wanted?.Trim());
        grid.Rebuild();
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

    private void AddRow(Panel host, PropertyDefinition definition, IReadOnlyList<object> targets, int depth,
        String wanted)
    {
        if (!definition.IsVisible) return;

        var searching = !String.IsNullOrEmpty(wanted);
        if (searching && !Answers(definition, wanted)) return;

        var row = new PropertyRow();
        row.Attach(this, definition, targets, depth * Indent);

        host.Children.Add(row);
        _rows.Add(row);

        if (definition is not CompositeProperty composite) return;

        // A folded composite opens while a search is running: its children are where the answer is, and leaving it shut
        // would hide what the search just found. A composite whose OWN name is the answer keeps all of its children.
        if (!composite.IsExpanded && !searching) return;

        var inside = searching && Carries(composite.Header as String, wanted) ? null : wanted;
        foreach (var child in composite.Children) AddRow(host, child, targets, depth + 1, inside);
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
