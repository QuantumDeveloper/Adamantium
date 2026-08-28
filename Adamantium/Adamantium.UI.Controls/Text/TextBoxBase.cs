using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Text;

/// <summary>
/// The editable-text CORE shared by every text input control. A TEMPLATED control (<see cref="Control"/>): the theme
/// supplies the chrome (a Border + a <see cref="TextPresenter"/> named <c>PART_TextPresenter</c>), and this class owns
/// the text buffer, caret + selection, keyboard navigation + editing, character input, the caret blink, clipboard, and a
/// caret-following horizontal scroll offset. The presenter is a thin surface that just measures / renders / hit-tests by
/// calling back here, so a concrete control (<see cref="TextBox"/>) or a future rich editor reuses ALL of this and only
/// its template changes.
/// </summary>
public abstract class TextBoxBase : Control
{
    // --- Text + editing state ------------------------------------------------------------------------------------

    public static readonly AdamantiumProperty TextProperty = AdamantiumProperty.Register(nameof(Text),
        typeof(string), typeof(TextBoxBase),
        new PropertyMetadata(string.Empty,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly AdamantiumProperty CaretIndexProperty = AdamantiumProperty.Register(nameof(CaretIndex),
        typeof(int), typeof(TextBoxBase), new PropertyMetadata(0, OnCaretOrSelectionChanged));

    public static readonly AdamantiumProperty SelectionStartProperty = AdamantiumProperty.Register(nameof(SelectionStart),
        typeof(int), typeof(TextBoxBase), new PropertyMetadata(0, OnCaretOrSelectionChanged));

    public static readonly AdamantiumProperty SelectionLengthProperty = AdamantiumProperty.Register(nameof(SelectionLength),
        typeof(int), typeof(TextBoxBase), new PropertyMetadata(0, OnCaretOrSelectionChanged));

    public static readonly AdamantiumProperty IsReadOnlyProperty = AdamantiumProperty.Register(nameof(IsReadOnly),
        typeof(bool), typeof(TextBoxBase), new PropertyMetadata(false));

    public static readonly AdamantiumProperty MaxLengthProperty = AdamantiumProperty.Register(nameof(MaxLength),
        typeof(int), typeof(TextBoxBase), new PropertyMetadata(0));   // 0 = unlimited

    public static readonly AdamantiumProperty TextWrappingProperty = AdamantiumProperty.Register(nameof(TextWrapping),
        typeof(TextWrapping), typeof(TextBoxBase),
        new PropertyMetadata(TextWrapping.NoWrap, PropertyMetadataOptions.AffectsMeasure, OnLayoutAffectingChanged));

    public static readonly AdamantiumProperty PlaceholderProperty = AdamantiumProperty.Register(nameof(Placeholder),
        typeof(string), typeof(TextBoxBase), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty FloatingPlaceholderProperty = AdamantiumProperty.Register(nameof(FloatingPlaceholder),
        typeof(bool), typeof(TextBoxBase),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure, OnLayoutAffectingChanged));

    // --- Presentation (Background + Foreground are inherited from Control) ----------------------------------------

    public static readonly AdamantiumProperty PlaceholderForegroundProperty = AdamantiumProperty.Register(nameof(PlaceholderForeground),
        typeof(Brush), typeof(TextBoxBase), new PropertyMetadata(Brushes.Gray, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty FloatingPlaceholderForegroundProperty = AdamantiumProperty.Register(nameof(FloatingPlaceholderForeground),
        typeof(Brush), typeof(TextBoxBase), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty CaretBrushProperty = AdamantiumProperty.Register(nameof(CaretBrush),
        typeof(Brush), typeof(TextBoxBase), new PropertyMetadata(Brushes.White, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty SelectionBrushProperty = AdamantiumProperty.Register(nameof(SelectionBrush),
        typeof(Brush), typeof(TextBoxBase), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    static TextBoxBase()
    {
        FontFamilyProperty.OverrideMetadata(typeof(TextBoxBase),
            new PropertyMetadata(null, PropertyMetadataOptions.Inherits, (a, _) => (a as TextBoxBase)?.OnFontChanged()));
        // An editor is a keyboard-focus target - opt in (the base default is now false). Its inner TextPresenter stays
        // non-focusable, so the focus walk from a click on the surface lands here, on the TextBox.
        FocusableProperty.OverrideMetadata(typeof(TextBoxBase), new PropertyMetadata(true));

        // The chrome properties are Control's; an editor only differs in what it defaults them TO - a framed box with
        // room around its text, rather than Control's bare zeroes.
        BorderThicknessProperty.OverrideMetadata(typeof(TextBoxBase),
            new PropertyMetadata(new Thickness(1), PropertyMetadataOptions.AffectsMeasure));
        PaddingProperty.OverrideMetadata(typeof(TextBoxBase),
            new PropertyMetadata(new Thickness(8, 4, 8, 4), PropertyMetadataOptions.AffectsMeasure));

        // Scroll-bar policy uses the shared ScrollViewer.* ATTACHED properties (no per-control duplicates). An editor
        // defaults to Hidden on BOTH axes - a single-line box scrolls its caret into view WITHOUT ever showing a bar - and
        // its OnScrollSettingChanged callback re-forwards the policy to PART_ContentHost (see PushScrollSettings, which also
        // forces the horizontal axis to Disabled while the text wraps). ListBox/etc. keep the base Disabled/Auto defaults.
        ScrollViewer.HorizontalScrollBarVisibilityProperty.OverrideMetadata(typeof(TextBoxBase),
            new PropertyMetadata(ScrollBarVisibility.Hidden, OnScrollSettingChanged));
        ScrollViewer.VerticalScrollBarVisibilityProperty.OverrideMetadata(typeof(TextBoxBase),
            new PropertyMetadata(ScrollBarVisibility.Hidden, OnScrollSettingChanged));
    }

    protected TextBoxBase()
    {
        AddHandler(Keyboard.TextInputEvent, new TextInputEventHandler(OnTextInputHandler));
    }

    public string Text
    {
        get => GetValue<string>(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Caret position as a character index in <see cref="Text"/> (0..Length). It is the moving end of the selection.</summary>
    public int CaretIndex
    {
        get => GetValue<int>(CaretIndexProperty);
        set => SetValue(CaretIndexProperty, value);
    }

    public int SelectionStart
    {
        get => GetValue<int>(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    public int SelectionLength
    {
        get => GetValue<int>(SelectionLengthProperty);
        set => SetValue(SelectionLengthProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Maximum number of characters; 0 (default) = unlimited.</summary>
    public int MaxLength
    {
        get => GetValue<int>(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>
    /// Soft wrapping of long lines at the viewport width. <see cref="TextWrapping.NoWrap"/> (default) keeps each line
    /// on one row (it scrolls horizontally to follow the caret); any wrapping value lays overflowing text onto extra
    /// rows. Independent of hard newlines - a control can accept <c>\n</c> (see <see cref="AcceptsNewLines"/>) with or
    /// without soft wrapping.
    /// </summary>
    public TextWrapping TextWrapping
    {
        get => GetValue<TextWrapping>(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    /// <summary>Scroll-bar policy on the horizontal axis (the shared <see cref="ScrollViewer.HorizontalScrollBarVisibilityProperty"/>
    /// attached property, forwarded to the internal ScrollViewer). Ignored while the box
    /// wraps - a wrapping box never scrolls horizontally.</summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => ScrollViewer.GetHorizontalScrollBarVisibility(this);
        set => ScrollViewer.SetHorizontalScrollBarVisibility(this, value);
    }

    /// <summary>Scroll-bar policy on the vertical axis (the shared <see cref="ScrollViewer.VerticalScrollBarVisibilityProperty"/>
    /// attached property, forwarded to the internal ScrollViewer). Use <c>Auto</c> for a multi-line box so a bar appears
    /// once the text outgrows the height.</summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => ScrollViewer.GetVerticalScrollBarVisibility(this);
        set => ScrollViewer.SetVerticalScrollBarVisibility(this, value);
    }

    /// <summary>Prompt text shown (in <see cref="PlaceholderForeground"/>) while the control is empty and unfocused.</summary>
    public string Placeholder
    {
        get => GetValue<string>(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public Brush PlaceholderForeground
    {
        get => GetValue<Brush>(PlaceholderForegroundProperty);
        set => SetValue(PlaceholderForegroundProperty, value);
    }

    /// <summary>When true, the <see cref="Placeholder"/> stays visible while typing: it animates up into a reserved strip
    /// above the text and shrinks (the Material / MahApps floating-label effect). When false (default) the placeholder is
    /// the classic in-line prompt shown only while empty and unfocused.</summary>
    public bool FloatingPlaceholder
    {
        get => GetValue<bool>(FloatingPlaceholderProperty);
        set => SetValue(FloatingPlaceholderProperty, value);
    }

    /// <summary>Colour of the floated (raised) label - typically an accent. The label's colour blends from
    /// <see cref="PlaceholderForeground"/> (resting) to this as it floats up. Falls back to PlaceholderForeground when null.</summary>
    public Brush FloatingPlaceholderForeground
    {
        get => GetValue<Brush>(FloatingPlaceholderForegroundProperty);
        set => SetValue(FloatingPlaceholderForegroundProperty, value);
    }

    public Brush CaretBrush
    {
        get => GetValue<Brush>(CaretBrushProperty);
        set => SetValue(CaretBrushProperty, value);
    }

    /// <summary>Highlight behind the selected text. Null falls back to a translucent default.</summary>
    public Brush SelectionBrush
    {
        get => GetValue<Brush>(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    // --- Template wiring -----------------------------------------------------------------------------------------

    private TextPresenter _presenter;
    private IMeasurableComponent _border;   // PART_Border - between the box and the presenter; must be re-measured too
    private ScrollViewer _scrollViewer;      // PART_ContentHost - owns the scroll offset + bars around the presenter

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_presenter != null) _presenter.Owner = null;
        _presenter = GetTemplateChild("PART_TextPresenter") as TextPresenter;
        _border = GetTemplateChild("PART_Border") as IMeasurableComponent;
        _scrollViewer = GetTemplateChild("PART_ContentHost") as ScrollViewer;
        if (_presenter != null) _presenter.Owner = this;
        PushScrollSettings();
        InvalidateSurface(measure: true);
    }

    // The presenter renders our text; changes to our state must re-render (and text changes re-measure) THAT element. On
    // a measure change we also invalidate the intervening Border: it is measure-valid and would otherwise GATE, so the
    // box would re-measure against the Border's cached (e.g. strip-less) desired and never see the presenter's new size.
    private void InvalidateSurface(bool measure = false)
    {
        if (_presenter == null) return;
        if (measure)
        {
            _presenter.InvalidateMeasure();
            _border?.InvalidateMeasure();
            InvalidateMeasure();
        }
        _presenter.InvalidateRender(false);
    }

    // --- Text layout (mirrors TextBlock's cached shaping) --------------------------------------------------------

    private TextLayout _textLayout;
    private FontFamily _layoutFont;
    private GlyphWordData[] _glyphs = [];      // shaped glyphs in text order; with newline sentinels, glyph i == character i
    private double _textWidth;                 // widest line's ink width (horizontal scroll bound in NoWrap)
    private double _lineHeight;
    private double _glyphLineHeight;           // real single-line ink extent (ascent+descent+gap) - reserves the LAST
                                               // line's descent so hanging tails (g y p q j) aren't clipped by the control
    private double _inkTopInLine;              // ascent line, measured from the line's top: where the glyphs of that line
    private double _inkHeight;                 // actually start, and how tall they are (ascent+descent)

    // Caret model, rebuilt on every (re)shape. For each of the TextLength+1 caret slots (slot i = caret BEFORE character
    // i; slot TextLength = the end) its text-local X and visual line index. Built from the shaped glyphs (one per
    // character incl. the zero-width newline sentinel), so it survives soft wrapping AND hard newlines uniformly.
    private double[] _caretX = [0];
    private int[] _caretLine = [0];
    private int _lineCount = 1;

    private string _lastShapedText;
    private double _lastShapedFontSize = -1;
    private double _lastShapedWidth = double.NaN;
    private TextWrapping _lastShapedWrapping = TextWrapping.NoWrap;
    private double _wrapWidth = double.PositiveInfinity;   // live viewport width for soft wrap; set by measure/render

    private const double CaretWidth = 1.0;
    private const double BlinkSeconds = 0.53;
    private const double CaretPadding = 2.0;   // keep the caret this far from the viewport edge when auto-scrolling

    private bool _caretVisible = true;
    private bool _caretSuppressed;   // an owner has taken the press for something that is not typing - see SuppressCaret
    private bool _blinking;
    private double _blinkAccum;
    private double _textOy;                     // vertical offset of the text within the surface (float strip + single-line centring)
    private double? _desiredColumnX;            // sticky X for Up/Down navigation (WPF behaviour); cleared by any horizontal move

    private double _floatProgress;             // floating placeholder: 0 = resting (in text), 1 = floated (small, top strip)
    private bool _floatAnimating;
    private bool _floatLoaded;
    private const double FloatScale = 0.75;    // floated label font size = FloatScale * FontSize
    private const double FloatSeconds = 0.14;  // float in/out animation duration

    // Undo / redo: snapshots of (text + caret + selection) taken BEFORE each mutation. Consecutive typed characters
    // coalesce into one entry (a "typing run"); deletes, paste, newline and every caret move break the run so each is
    // its own undo step. `_restoring` guards the stacks while a snapshot is being re-applied.
    private readonly record struct TextState(string Text, int Caret, int SelStart, int SelLength);
    private readonly List<TextState> _undo = [];
    private readonly List<TextState> _redo = [];
    private bool _typingRun;
    private bool _restoring;
    private const int UndoLimit = 500;

    private void OnFontChanged()
    {
        _lastShapedText = null;
        InvalidateMeasure();
        InvalidateSurface(measure: true);
    }

    private void EnsureLayout()
    {
        var font = FontFamily ?? DefaultFontFamily;
        if (_textLayout == null || !ReferenceEquals(_layoutFont, font))
        {
            _textLayout = new TextLayout(font.Typeface, font.Fonts[0]) { EmitNewlineCarets = true };
            _layoutFont = font;
            _lastShapedText = null;
        }

        // Match ProcessText's own line advance EXACTLY (it places each line this far below the previous). Deriving the
        // caret Y from a different guess (e.g. 1.4 * FontSize) made the caret drift off its line onto the next row.
        var iFont = _textLayout.Font;
        var lgScale = FontSize / iFont.UnitsPerEm;
        _lineHeight = (iFont.LineGap == 0 ? FontSize : iFont.LineGap * lgScale) + FontSize;
        // ProcessText places each line's baseline at Font.Baseline*scale from the line top (NOT ascent - this font's
        // Baseline is ~the full line box) and lets descenders "hang below" without reserving space, so a single-line
        // field's tails overflow _lineHeight and the control's clip cuts them. Reserve the TRUE bottom of the last line's
        // ink - its baseline (Baseline*scale) plus the descent below it - never less than the line advance.
        _glyphLineHeight = Math.Max(_lineHeight, (iFont.Baseline + Math.Abs(iFont.Descender)) * lgScale);
        // Where that line's GLYPHS are, which is not where the line BOX is: ProcessText puts the baseline
        // Baseline*scale below the line top and the ink spans ascent..descent around it. For Segoe UI at 12 the line box
        // is 0..13.6 while the ink is 4.5..15.8 - so anything drawn on the box (the caret, the selection) sits a couple
        // of pixels ABOVE the text it is supposed to mark. Both are derived from here instead.
        _inkTopInLine = (iFont.Baseline - iFont.Ascender) * lgScale;
        _inkHeight = (iFont.Ascender + Math.Abs(iFont.Descender)) * lgScale;

        var text = Text ?? string.Empty;
        var wrapping = TextWrapping;
        // NaN width => ProcessText treats the axis as unbounded and returns the real ink extent (its explicit
        // "unbounded" sentinel). Soft wrap passes the live viewport width so lines break there.
        var width = wrapping == TextWrapping.NoWrap || _wrapWidth <= 0 || double.IsInfinity(_wrapWidth)
            ? double.NaN
            : _wrapWidth;

        if (_lastShapedText == text && _lastShapedFontSize.Equals(FontSize)
            && _lastShapedWrapping == wrapping && _lastShapedWidth.Equals(width))
            return;

        if (text.Length == 0)
        {
            _glyphs = [];
            _textWidth = 0;
            _caretX = [0];
            _caretLine = [0];
            _lineCount = 1;
        }
        else
        {
            var size = _textLayout.ProcessText(text, FontSize,
                new Size(width, double.NaN),
                wrapping, TextTrimming.None,
                HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
            _glyphs = _textLayout.GetTextData();   // text order, one glyph per character (newline = zero-width sentinel)
            _textWidth = size.Width;
            BuildCaretModel(text);
        }

        _lastShapedText = text;
        _lastShapedFontSize = FontSize;
        _lastShapedWrapping = wrapping;
        _lastShapedWidth = width;
    }

    // Populate _caretX/_caretLine (length TextLength+1) from the shaped glyphs. Relies on the shaper emitting exactly one
    // glyph per character (letters, spaces and the newline sentinel) in text order, so glyph i belongs to character i. If
    // the counts ever disagree we fall back to a single line so the control stays usable instead of throwing.
    private void BuildCaretModel(string text)
    {
        var n = text.Length;
        _caretX = new double[n + 1];
        _caretLine = new int[n + 1];

        if (_glyphs.Length != n)
        {
            for (var i = 0; i <= n; i++) { _caretX[i] = i < _glyphs.Length ? _glyphs[i].Rect.X : _textWidth; _caretLine[i] = 0; }
            _lineCount = 1;
            return;
        }

        var maxLine = 0;
        for (var i = 0; i < n; i++)
        {
            _caretX[i] = _glyphs[i].Rect.X;
            _caretLine[i] = _glyphs[i].LineIndex;
            if (_glyphs[i].LineIndex > maxLine) maxLine = _glyphs[i].LineIndex;
        }

        // End slot: after a trailing newline the caret starts a fresh line below; otherwise it sits just past the last
        // glyph's right edge on that glyph's line.
        var last = _glyphs[n - 1];
        if (last.Symbol == '\n') { _caretX[n] = 0; _caretLine[n] = last.LineIndex + 1; maxLine = Math.Max(maxLine, last.LineIndex + 1); }
        else { _caretX[n] = last.Rect.Right; _caretLine[n] = last.LineIndex; }

        _lineCount = maxLine + 1;
    }

    // --- Caret / selection geometry ------------------------------------------------------------------------------

    private int MaxLineIndex => _lineCount - 1;
    // Lines advance by _lineHeight; the LAST line reserves its full ink height (ascent+descent) so hanging descenders
    // aren't clipped by the control's bottom edge (the em-based advance can be shorter than ascent+descent).
    private double ContentHeight => (_lineCount - 1) * _lineHeight + _glyphLineHeight;

    // Text-local caret rect (before the scroll offset) sitting BEFORE character index: X from the caret model, Y from the
    // line index. It spans the line's INK (ascent to descent), not its line box - a caret marks where the letters are.
    internal Rect CaretRect(int index)
    {
        EnsureLayout();
        index = Math.Clamp(index, 0, _caretX.Length - 1);
        return new Rect(_caretX[index], _caretLine[index] * _lineHeight + _inkTopInLine, CaretWidth, _inkHeight);
    }

    private int CaretLineOf(int index)
    {
        EnsureLayout();
        return _caretLine[Math.Clamp(index, 0, _caretLine.Length - 1)];
    }

    // First / last caret slot index on a given visual line (slots for a line are a contiguous run, text order).
    private (int first, int last) LineSlotRange(int line)
    {
        int first = -1, last = -1;
        for (var i = 0; i < _caretLine.Length; i++)
        {
            if (_caretLine[i] != line) { if (first >= 0) break; continue; }
            if (first < 0) first = i;
            last = i;
        }
        return first < 0 ? (0, _caretX.Length - 1) : (first, last);
    }

    // Nearest caret index to a text-local point: pick the visual line by Y, then the slot on it nearest X (splitting on
    // glyph mid-points like a text cursor). Empty lines are handled because their single caret slot carries the line.
    private int IndexFromPoint(double x, double y)
    {
        EnsureLayout();
        if (_caretX.Length == 1) return 0;

        var line = Math.Clamp((int)Math.Floor(y / _lineHeight), 0, MaxLineIndex);
        var (first, last) = LineSlotRange(line);
        for (var i = first; i <= last; i++)
        {
            if (i >= _glyphs.Length) return i;              // end-of-text slot on this line
            if (_glyphs[i].Symbol == '\n') return i;        // click past the visible end -> before the line's newline
            if (x < _caretX[i] + _glyphs[i].Rect.Width / 2.0) return i;
        }
        return last;
    }

    private int TextLength => (Text ?? string.Empty).Length;

    private bool HasSelection => SelectionLength > 0;

    private int Clamp(int index) => Math.Clamp(index, 0, TextLength);

    private (int start, int end) SelectionRange()
    {
        var s = Clamp(SelectionStart);
        var e = Clamp(SelectionStart + SelectionLength);
        return s <= e ? (s, e) : (e, s);
    }

    // --- Editing operations --------------------------------------------------------------------------------------

    private void MoveCaretTo(int newIndex, bool extend, bool keepColumn = false)
    {
        newIndex = Clamp(newIndex);
        if (!keepColumn) _desiredColumnX = null;   // any non-vertical move drops the sticky Up/Down column
        _typingRun = false;                        // a caret move ends the current typing run (its own undo step)
        if (extend)
        {
            var anchor = SelectionLength == 0 ? CaretIndex : SelectionAnchor();
            SetSelection(anchor, newIndex);
        }
        else
        {
            SelectionStart = newIndex;
            SelectionLength = 0;
        }
        CaretIndex = newIndex;
        ResetBlink();
        ScrollCaretIntoView();
    }

    // Move the caret one visual line up/down, preserving the sticky column X (WPF behaviour). Past the first/last line
    // it snaps to the document start/end.
    private void MoveCaretVertical(int dir, bool extend)
    {
        EnsureLayout();
        var target = CaretLineOf(CaretIndex) + dir;
        if (target < 0) { MoveCaretTo(0, extend); return; }
        if (target > MaxLineIndex) { MoveCaretTo(TextLength, extend); return; }

        var col = _desiredColumnX ?? CaretRect(CaretIndex).X;
        var idx = IndexFromPoint(col, target * _lineHeight + _lineHeight / 2.0);
        MoveCaretTo(idx, extend, keepColumn: true);
        _desiredColumnX = col;
    }

    // Start / end of the caret's current VISUAL line (soft-wrapped rows count as separate lines, like WPF).
    private int CurrentLineStart() { var (f, _) = LineSlotRange(CaretLineOf(CaretIndex)); return f; }
    private int CurrentLineEnd() { var (_, l) = LineSlotRange(CaretLineOf(CaretIndex)); return l; }

    private int SelectionAnchor()
    {
        var (s, e) = SelectionRange();
        return CaretIndex == e ? s : e;
    }

    private void SetSelection(int anchor, int caret)
    {
        SelectionStart = Math.Min(anchor, caret);
        SelectionLength = Math.Abs(caret - anchor);
    }

    public void SelectAll()
    {
        SelectionStart = 0;
        SelectionLength = TextLength;
        CaretIndex = TextLength;
    }

    /// <summary>Replace the selection (or insert at the caret when there is none) with <paramref name="insert"/>.
    /// <paramref name="isTyping"/> lets consecutive single characters coalesce into one undo step.</summary>
    protected void ReplaceSelection(string insert, bool isTyping = false)
    {
        if (IsReadOnly) return;
        insert ??= string.Empty;

        var text = Text ?? string.Empty;
        var (start, end) = HasSelection ? SelectionRange() : (Clamp(CaretIndex), Clamp(CaretIndex));

        if (MaxLength > 0)
        {
            var room = MaxLength - (text.Length - (end - start));
            if (room <= 0 && insert.Length > 0) insert = string.Empty;
            else if (insert.Length > room) insert = insert.Substring(0, Math.Max(0, room));
        }

        var newText = text.Substring(0, start) + insert + text.Substring(end);
        if (newText == text) return;   // nothing actually changed -> no edit, no undo entry

        RecordUndo(isTyping);

        // SetCurrentValue, NOT `Text = ` (which is a Local-priority set): an edit must write in the BINDING's own slot so a
        // two-way {Binding}/{Ancestor} fires its write-back AND later source->target updates still apply. A plain Local set
        // MASKS the binding - the box then "lives on its own", never reflecting a later source change (a ColorPicker field
        // stuck after you typed in it). Mirrors Slider.SetValueFromInput.
        SetCurrentValue(TextProperty, newText);
        var newCaret = start + insert.Length;
        SelectionStart = newCaret;
        SelectionLength = 0;
        CaretIndex = newCaret;
        ResetBlink();
        ScrollCaretIntoView();
    }

    // --- Undo / redo ---------------------------------------------------------------------------------------------

    private TextState Snapshot() => new(Text ?? string.Empty, CaretIndex, SelectionStart, SelectionLength);

    // Capture the PRE-edit state. A typing run keeps only its first snapshot (so undo removes the whole run at once);
    // any non-typing edit, or the first char after a caret move, starts a fresh entry. Every edit clears the redo stack.
    private void RecordUndo(bool isTyping)
    {
        if (_restoring) return;
        _redo.Clear();
        if (isTyping && _typingRun) return;
        _undo.Add(Snapshot());
        if (_undo.Count > UndoLimit) _undo.RemoveAt(0);
        _typingRun = isTyping;
    }

    public void Undo()
    {
        if (IsReadOnly || _undo.Count == 0) return;
        _redo.Add(Snapshot());
        var s = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        RestoreState(s);
    }

    public void Redo()
    {
        if (IsReadOnly || _redo.Count == 0) return;
        _undo.Add(Snapshot());
        var s = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        RestoreState(s);
    }

    private void RestoreState(TextState s)
    {
        _restoring = true;
        _typingRun = false;
        SetCurrentValue(TextProperty, s.Text);   // binding-slot write (see ReplaceSelection), so undo doesn't mask a two-way binding either
        var len = TextLength;
        SelectionStart = Math.Clamp(s.SelStart, 0, len);
        SelectionLength = Math.Clamp(s.SelLength, 0, len - Math.Clamp(s.SelStart, 0, len));
        CaretIndex = Math.Clamp(s.Caret, 0, len);
        _restoring = false;
        ResetBlink();
    }

    private void DeleteBackward()
    {
        if (IsReadOnly) return;
        if (HasSelection) { ReplaceSelection(string.Empty); return; }
        if (CaretIndex <= 0) return;
        SelectionStart = CaretIndex - 1;
        SelectionLength = 1;
        ReplaceSelection(string.Empty);
    }

    private void DeleteForward()
    {
        if (IsReadOnly) return;
        if (HasSelection) { ReplaceSelection(string.Empty); return; }
        if (CaretIndex >= TextLength) return;
        SelectionStart = CaretIndex;
        SelectionLength = 1;
        ReplaceSelection(string.Empty);
    }

    private int WordBoundary(int index, int dir)
    {
        var text = Text ?? string.Empty;
        var i = index;
        if (dir < 0)
        {
            while (i > 0 && char.IsWhiteSpace(text[i - 1])) i--;
            while (i > 0 && !char.IsWhiteSpace(text[i - 1])) i--;
        }
        else
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
        }
        return i;
    }

    // --- Input: keyboard + characters ----------------------------------------------------------------------------

    private void OnTextInputHandler(object sender, TextInputEventArgs e)
    {
        if (IsReadOnly || string.IsNullOrEmpty(e.Text)) return;
        var filtered = new string(e.Text.Where(c => !char.IsControl(c)).ToArray());
        if (filtered.Length == 0) return;
        ReplaceSelection(filtered, isTyping: true);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var ctrl = (Keyboard.Modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;
        var shift = (Keyboard.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;

        switch (e.Key)
        {
            case Key.LeftArrow:
                MoveCaretTo(ctrl ? WordBoundary(CaretIndex, -1) : CaretIndex - 1, shift);
                e.Handled = true;
                break;
            case Key.RightArrow:
                MoveCaretTo(ctrl ? WordBoundary(CaretIndex, +1) : CaretIndex + 1, shift);
                e.Handled = true;
                break;
            case Key.UpArrow:
                MoveCaretVertical(-1, shift);
                e.Handled = true;
                break;
            case Key.DownArrow:
                MoveCaretVertical(+1, shift);
                e.Handled = true;
                break;
            case Key.Home:
                MoveCaretTo(ctrl ? 0 : CurrentLineStart(), shift);
                e.Handled = true;
                break;
            case Key.End:
                MoveCaretTo(ctrl ? TextLength : CurrentLineEnd(), shift);
                e.Handled = true;
                break;
            case Key.BackSpace:
                if (ctrl && !HasSelection) { SelectionStart = WordBoundary(CaretIndex, -1); SelectionLength = CaretIndex - SelectionStart; }
                DeleteBackward();
                e.Handled = true;
                break;
            case Key.Delete:
                if (ctrl && !HasSelection) { SelectionStart = CaretIndex; SelectionLength = WordBoundary(CaretIndex, +1) - CaretIndex; }
                DeleteForward();
                e.Handled = true;
                break;
            case Key.Z when ctrl && shift:   // Ctrl+Shift+Z = redo (common alias)
                Redo();
                e.Handled = true;
                break;
            case Key.Z when ctrl:
                Undo();
                e.Handled = true;
                break;
            case Key.Y when ctrl:
                Redo();
                e.Handled = true;
                break;
            case Key.A when ctrl:
                SelectAll();
                e.Handled = true;
                break;
            case Key.C when ctrl:
                CopyToClipboard();
                e.Handled = true;
                break;
            case Key.X when ctrl:
                CutToClipboard();
                e.Handled = true;
                break;
            case Key.V when ctrl:
                PasteFromClipboard();
                e.Handled = true;
                break;
            default:
                OnUnhandledKey(e);
                break;
        }
    }

    /// <summary>Keys not handled by the shared editing map (e.g. Enter) - a concrete control overrides to react.</summary>
    protected virtual void OnUnhandledKey(KeyEventArgs e) { }

    /// <summary>Whether newline characters are kept in the buffer (multi-line control). When false, pasted newlines
    /// collapse to spaces. A concrete control ties this to its own AcceptsReturn-style option.</summary>
    protected virtual bool AcceptsNewLines => false;

    protected virtual void CopyToClipboard()
    {
        if (HasSelection) Clipboard.SetText(SelectedText());
    }

    protected virtual void CutToClipboard()
    {
        if (IsReadOnly || !HasSelection) return;
        Clipboard.SetText(SelectedText());
        ReplaceSelection(string.Empty);
    }

    protected virtual void PasteFromClipboard()
    {
        if (IsReadOnly) return;
        var text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");       // normalise line endings
        if (!AcceptsNewLines) text = text.Replace("\n", " ");        // single-line: newlines become spaces
        ReplaceSelection(text);
    }

    protected string SelectedText()
    {
        var (s, e) = SelectionRange();
        return (Text ?? string.Empty).Substring(s, e - s);
    }

    // --- Surface callbacks (the TextPresenter measures / renders / hit-tests through these) -----------------------

    internal Size MeasureSurface(double availableWidth)
    {
        _wrapWidth = availableWidth;   // soft wrap breaks at the viewport width
        EnsureLayout();
        // Wrapped text fills the given width; unwrapped desires its widest line (+ caret). Height is all the lines plus
        // the reserved strip for a floating label (zero when that effect is off).
        var w = TextWrapping != TextWrapping.NoWrap && !double.IsInfinity(availableWidth)
            ? availableWidth
            : _textWidth + CaretWidth;
        return new Size(w, ContentHeight + FloatStripHeight());
    }

    internal void SurfaceMouseDown(double localX, double localY, bool extend)
    {
        Focus();
        MoveCaretTo(IndexFromPoint(localX, localY - _textOy), extend);
    }

    internal void SurfaceMouseMove(double localX, double localY)
        => MoveCaretTo(IndexFromPoint(localX, localY - _textOy), extend: true);

    /// <summary>Double-click: take the word under the point (or the run of spaces, when the click lands between words -
    /// a gesture that selects nothing at all reads as a dead click).</summary>
    internal void SurfaceSelectWord(double localX, double localY)
    {
        Focus();
        SelectWordAt(IndexFromPoint(localX, localY - _textOy));
    }

    /// <summary>Selects the word containing <paramref name="index"/> - what a double-click does, and what anything else
    /// that wants a word rather than a character can call.</summary>
    public void SelectWordAt(int index)
    {
        var (start, end) = WordAt(index);
        SelectionStart = start;
        SelectionLength = end - start;
        CaretIndex = end;
    }

    /// <summary>Triple-click: everything.</summary>
    internal void SurfaceSelectAll()
    {
        Focus();
        SelectAll();
    }

    /// <summary>Drop a mouse selection that is still in progress - see <see cref="TextPresenter.CancelSelection"/>.</summary>
    internal void CancelMouseSelection()
    {
        _presenter?.CancelSelection();
        SelectionLength = 0;
    }

    /// <summary>Hide the caret while an owner has taken the press for something that is not typing - a NumericUpDown
    /// running its value under a drag. The box keeps focus (the keys still belong to it the moment the drag ends), but a
    /// caret blinking through the drag claims it is being edited as text, and points at a place in a number that is
    /// being replaced several times a second.</summary>
    internal void SuppressCaret(bool suppress)
    {
        if (_caretSuppressed == suppress) return;

        _caretSuppressed = suppress;
        if (suppress) InvalidateSurface();
        else ResetBlink();   // back from the drag: show it at once rather than mid-blink
    }

    // The run around an index: a word, or the whitespace between two. Not WordBoundary() twice - that one walks OVER the
    // gap to the next word, so a click at a word's first character would have selected the word BEFORE it.
    private (int Start, int End) WordAt(int index)
    {
        var text = Text ?? string.Empty;
        if (text.Length == 0) return (0, 0);

        var i = Math.Clamp(index, 0, text.Length - 1);
        var inWhitespace = char.IsWhiteSpace(text[i]);
        var start = i;
        var end = i;
        while (start > 0 && char.IsWhiteSpace(text[start - 1]) == inWhitespace) start--;
        while (end < text.Length && char.IsWhiteSpace(text[end]) == inWhitespace) end++;
        return (start, end);
    }

    internal void RenderSurface(IDrawingSession session, Size size)
    {
        EnsureLayout();

        // First time the field is actually drawn: all initial bindings have settled, so snap the floating label to its
        // resolved state (floated if it already holds text) WITHOUT animating - only later user changes animate.
        if (!_floatLoaded) { _floatLoaded = true; _floatProgress = FloatTarget(); }

        var hasText = !string.IsNullOrEmpty(Text);
        // Scrolling is owned by the enclosing ScrollViewer (its ScrollContentPresenter translates this whole surface by
        // the offset), so we render the FULL content at our own origin (ox = 0). Text sits below the floating-label strip
        // (zero when off); single-line content then centres vertically in the surface (which the ScrollContentPresenter
        // sizes to at least the viewport), multi-line is top-aligned. vOffset is 0 for the multi-line case.
        const double ox = 0;
        var stripH = FloatStripHeight();
        var textAreaH = size.Height - stripH;
        var vOffset = !AcceptsNewLines && ContentHeight < textAreaH ? (textAreaH - ContentHeight) / 2 : 0;
        var oy = stripH + vOffset;
        _textOy = oy;

        // Selection highlight (behind the text) - one rect per spanned visual line.
        if (HasSelection) DrawSelection(session, ox, oy);

        // Text.
        if (hasText)
        {
            session.DrawText(BuildTextParameters(Foreground, ox, oy, size), size, _textLayout, Foreground, Brushes.Transparent, Brushes.Transparent);
        }

        // Placeholder: floating label (always visible, animates up/shrinks) or the classic in-line prompt (only while
        // empty and unfocused).
        if (!string.IsNullOrEmpty(Placeholder))
        {
            if (FloatingPlaceholder) RenderFloatingPlaceholder(session, oy, size);
            else if (!hasText && !IsFocused) RenderPlaceholder(session, oy, size);
        }

        // Caret.
        if (IsFocused && _caretVisible && !_caretSuppressed)
        {
            var c = CaretRect(CaretIndex);
            session.DrawRectangle(CaretBrush, new Rect(ox + c.X, oy + c.Y, CaretWidth, c.Height));
        }
    }

    private void DrawSelection(IDrawingSession session, double ox, double oy)
    {
        var (s, en) = SelectionRange();
        var brush = SelectionBrush ?? DefaultSelectionBrush;
        var startLine = CaretLineOf(s);
        var endLine = CaretLineOf(en);
        for (var line = startLine; line <= endLine; line++)
        {
            var (first, last) = LineSlotRange(line);
            var x0 = line == startLine ? _caretX[s] : _caretX[first];
            var x1 = line == endLine ? _caretX[en] : _caretX[last];
            var w = Math.Max(0, x1 - x0);
            if (line != endLine && w <= 0) w = _lineHeight * 0.4;   // sliver so a selected empty/blank line is visible
            session.DrawRectangle(brush, new Rect(ox + x0, oy + line * _lineHeight, w, _lineHeight));
        }
    }

    // Scroll the enclosing ScrollViewer the minimum needed to keep the caret visible (WPF caret-follow). The caret rect is
    // in text-layout coords; content coords add the vertical text offset (float strip + single-line centring). Horizontal
    // is 1:1 (ox = 0). No-op until the template's ScrollViewer exists / when the caret already fits.
    private void ScrollCaretIntoView()
    {
        if (_scrollViewer == null) return;
        var c = CaretRect(CaretIndex);
        _scrollViewer.BringIntoView(new Rect(c.X, c.Y + _textOy, CaretWidth + CaretPadding, c.Height));
    }

    private TextRenderingParameters BuildTextParameters(Brush color, double originX, double originY, Size size)
    {
        return new TextRenderingParameters
        {
            HorizontalTextAlignment = HorizontalTextAlignment.Left,
            VerticalTextAlignment = VerticalTextAlignment.Top,
            TextTrimming = TextTrimming.None,
            TextWrapping = TextWrapping,
            Color = (color as SolidColorBrush)?.Color ?? Colors.White,
            TextArea = new Rectangle(new Vector2F((float)originX, (float)originY), size)
        };
    }

    private TextLayout _placeholderLayout;
    private string _placeholderShaped;
    private void EnsurePlaceholderShaped(double fontSize)
    {
        var font = FontFamily ?? DefaultFontFamily;
        if (_placeholderLayout == null || !ReferenceEquals(_layoutFont, font)) _placeholderLayout = new TextLayout(font.Typeface, font.Fonts[0]);
        var key = Placeholder + "|" + fontSize;
        if (_placeholderShaped == key) return;
        _placeholderLayout.ProcessText(Placeholder, fontSize, new Size(double.NaN, double.NaN),
            TextWrapping.NoWrap, TextTrimming.None, HorizontalTextAlignment.Left, VerticalTextAlignment.Top);
        _placeholderShaped = key;
    }

    private void RenderPlaceholder(IDrawingSession session, double oy, Size size)
    {
        EnsurePlaceholderShaped(FontSize);
        session.DrawText(BuildTextParameters(PlaceholderForeground, 0, oy, size), size, _placeholderLayout, PlaceholderForeground, Brushes.Transparent, Brushes.Transparent);
    }

    // Floating label: interpolate font size (full -> shrunk), Y (text position -> top strip) and colour (placeholder ->
    // accent) by _floatProgress.
    private void RenderFloatingPlaceholder(IDrawingSession session, double textOy, Size size)
    {
        var t = _floatProgress;
        var floatFs = FontSize + (FontSize * FloatScale - FontSize) * t;
        EnsurePlaceholderShaped(floatFs);
        var y = textOy + (0.0 - textOy) * t;   // rest (t=0): at the text; floated (t=1): at the top of the strip
        var brush = FloatLabelBrush(t);
        session.DrawText(BuildTextParameters(brush, 0, y, size), size, _placeholderLayout, brush, Brushes.Transparent, Brushes.Transparent);
    }

    // Blend the label colour from the resting placeholder colour to the accent (FloatingPlaceholderForeground) as it
    // rises. Blends only when both are solid; otherwise switches at the half-way point.
    private Brush FloatLabelBrush(double t)
    {
        var rest = PlaceholderForeground;
        var accent = FloatingPlaceholderForeground ?? PlaceholderForeground;
        if (t <= 0 || ReferenceEquals(rest, accent)) return rest;
        if (t >= 1) return accent;
        if (rest is SolidColorBrush a && accent is SolidColorBrush b)
        {
            byte L(byte from, byte to) => (byte)(from + (to - from) * t);
            return new SolidColorBrush(new Color(L(a.Color.R, b.Color.R), L(a.Color.G, b.Color.G), L(a.Color.B, b.Color.B), L(a.Color.A, b.Color.A)));
        }
        return t < 0.5 ? rest : accent;
    }

    // --- Focus + caret blink -------------------------------------------------------------------------------------

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        StartBlink();
        UpdateFloatState();
        InvalidateSurface();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _caretVisible = false;
        UpdateFloatState();
        InvalidateSurface();
    }

    // --- Floating placeholder (Material / MahApps label) ---------------------------------------------------------

    // Height reserved above the text for the floated (shrunk) label. Zero unless the effect is on and there is a label.
    private double FloatStripHeight()
        => FloatingPlaceholder && !string.IsNullOrEmpty(Placeholder) ? Math.Ceiling(FontSize * FloatScale * 1.3) : 0;

    // The label floats up whenever the field is focused or non-empty; it returns to the text line (full size) only when
    // the field is both empty AND unfocused.
    private double FloatTarget() => IsFocused || !string.IsNullOrEmpty(Text) ? 1.0 : 0.0;

    private void UpdateFloatState()
    {
        var target = FloatTarget();
        // Before the field is first shown (initial bindings for Text/FloatingPlaceholder are still settling, in any
        // order) - or whenever the effect is off - keep the label at its RESOLVED position with no animation. Otherwise
        // (shown + effect on) a real change (focus, first char, last delete) animates. This fixes the label resting at
        // the bottom when the field loads already holding text: it must start floated, not slide up on the first click.
        if (!_floatLoaded || !FloatingPlaceholder) { _floatProgress = target; InvalidateSurface(); return; }
        if (Math.Abs(_floatProgress - target) > 1e-3) StartFloatAnim();
    }

    private void StartFloatAnim()
    {
        if (_floatAnimating) return;
        _floatAnimating = true;
        AnimationManager.AddTicker(dt =>
        {
            var target = FloatTarget();
            var step = dt / FloatSeconds;
            _floatProgress = _floatProgress < target
                ? Math.Min(target, _floatProgress + step)
                : Math.Max(target, _floatProgress - step);
            InvalidateSurface();
            if (Math.Abs(_floatProgress - target) < 1e-3) { _floatProgress = target; _floatAnimating = false; return true; }
            return false;
        });
    }

    private void StartBlink()
    {
        _caretVisible = true;
        _blinkAccum = 0;
        InvalidateSurface();
        if (_blinking) return;
        _blinking = true;
        AnimationManager.AddTicker(dt =>
        {
            if (!IsFocused) { _blinking = false; return true; }
            _blinkAccum += dt;
            if (_blinkAccum >= BlinkSeconds)
            {
                _blinkAccum -= BlinkSeconds;
                _caretVisible = !_caretVisible;
                InvalidateSurface();
            }
            return false;
        });
    }

    private void ResetBlink()
    {
        _blinkAccum = 0;
        _caretVisible = true;
        InvalidateSurface();
    }

    private static void OnTextChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not TextBoxBase tb) return;
        tb._lastShapedText = null;
        var len = (tb.Text ?? string.Empty).Length;
        if (tb.CaretIndex > len) tb.SetCurrentValue(CaretIndexProperty, len);
        if (tb.SelectionStart > len) tb.SetCurrentValue(SelectionStartProperty, len);
        if (tb.SelectionStart + tb.SelectionLength > len)
            tb.SetCurrentValue(SelectionLengthProperty, Math.Max(0, len - tb.SelectionStart));
        tb.UpdateFloatState();
        tb.InvalidateSurface(measure: true);
    }

    private static void OnCaretOrSelectionChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => (a as TextBoxBase)?.InvalidateSurface();

    // A property that changes how the text lays out (e.g. wrapping, the floating-label strip): drop the shaping cache,
    // resolve the label state, and re-measure (InvalidateSurface re-measures the box + Border + presenter so the strip
    // change actually reaches the presenter - see InvalidateSurface).
    private static void OnLayoutAffectingChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not TextBoxBase tb) return;
        tb._lastShapedText = null;
        tb.UpdateFloatState();
        tb.PushScrollSettings();   // TextWrapping toggles whether the horizontal axis is scrollable vs. wrapping
        tb.InvalidateSurface(measure: true);
    }

    private static void OnScrollSettingChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => (a as TextBoxBase)?.PushScrollSettings();

    // Forward our policy to the internal ScrollViewer. A wrapping box must NOT scroll horizontally (the content is bound
    // to the viewport width so it wraps), so we force Disabled there regardless of HorizontalScrollBarVisibility.
    private void PushScrollSettings()
    {
        if (_scrollViewer == null) return;
        _scrollViewer.HorizontalScrollBarVisibility =
            TextWrapping != TextWrapping.NoWrap ? ScrollBarVisibility.Disabled : HorizontalScrollBarVisibility;
        _scrollViewer.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
    }

    private static readonly Brush DefaultSelectionBrush = new SolidColorBrush(new Color(0x33, 0x99, 0xFF, 0x66));
}
