using System;
using System.Linq;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
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

    public static readonly AdamantiumProperty PlaceholderProperty = AdamantiumProperty.Register(nameof(Placeholder),
        typeof(string), typeof(TextBoxBase), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    // --- Presentation (Background + Foreground are inherited from Control) ----------------------------------------

    public static readonly AdamantiumProperty PlaceholderForegroundProperty = AdamantiumProperty.Register(nameof(PlaceholderForeground),
        typeof(Brush), typeof(TextBoxBase), new PropertyMetadata(Brushes.Gray, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
        typeof(Brush), typeof(TextBoxBase), new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty BorderThicknessProperty = AdamantiumProperty.Register(nameof(BorderThickness),
        typeof(Thickness), typeof(TextBoxBase), new PropertyMetadata(new Thickness(1), PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
        typeof(CornerRadius), typeof(TextBoxBase), new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
        typeof(Thickness), typeof(TextBoxBase),
        new PropertyMetadata(new Thickness(8, 4, 8, 4), PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty FontSizeProperty = AdamantiumProperty.Register(nameof(FontSize),
        typeof(double), typeof(TextBoxBase),
        new PropertyMetadata(14.0, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty CaretBrushProperty = AdamantiumProperty.Register(nameof(CaretBrush),
        typeof(Brush), typeof(TextBoxBase), new PropertyMetadata(Brushes.White, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty SelectionBrushProperty = AdamantiumProperty.Register(nameof(SelectionBrush),
        typeof(Brush), typeof(TextBoxBase), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    static TextBoxBase()
    {
        FontFamilyProperty.OverrideMetadata(typeof(TextBoxBase),
            new PropertyMetadata(null, PropertyMetadataOptions.Inherits, (a, _) => (a as TextBoxBase)?.OnFontChanged()));
    }

    protected TextBoxBase()
    {
        AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(OnKeyDownHandler));
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

    public Brush BorderBrush
    {
        get => GetValue<Brush>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue<CornerRadius>(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public double FontSize
    {
        get => GetValue<double>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
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

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_presenter != null) _presenter.Owner = null;
        _presenter = GetTemplateChild("PART_TextPresenter") as TextPresenter;
        if (_presenter != null) _presenter.Owner = this;
        InvalidateSurface(measure: true);
    }

    // The presenter renders our text; changes to our state must re-render (and text changes re-measure) THAT element.
    private void InvalidateSurface(bool measure = false)
    {
        if (_presenter == null) return;
        if (measure) _presenter.InvalidateMeasure();
        _presenter.InvalidateRender(false);
    }

    // --- Text layout (mirrors TextBlock's cached shaping) --------------------------------------------------------

    private TextLayout _textLayout;
    private FontFamily _layoutFont;
    private GlyphWordData[] _glyphs = [];      // shaped glyphs in text order (index i == character i for single line)
    private double _textWidth;                 // measured ink width (right-edge caret position)
    private double _lineHeight;
    private string _lastShapedText;
    private double _lastShapedFontSize = -1;

    private const double CaretWidth = 1.0;
    private const double BlinkSeconds = 0.53;
    private const double CaretPadding = 2.0;   // keep the caret this far from the viewport edge when auto-scrolling

    private bool _caretVisible = true;
    private bool _blinking;
    private double _blinkAccum;
    private double _scrollOffset;              // horizontal, in text-local px; keeps the caret inside the viewport

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
            _textLayout = new TextLayout(font.Typeface, font.Fonts[0]);
            _layoutFont = font;
            _lastShapedText = null;
        }

        _lineHeight = Math.Ceiling(FontSize * 1.4);

        var text = Text ?? string.Empty;
        if (_lastShapedText == text && _lastShapedFontSize.Equals(FontSize)) return;

        if (text.Length == 0)
        {
            _glyphs = [];
            _textWidth = 0;
        }
        else
        {
            var size = _textLayout.ProcessText(text, FontSize,
                new Size(double.PositiveInfinity, _lineHeight),
                TextWrapping.NoWrap, TextTrimming.None,
                HorizontalTextAlignment.Left, VerticalTextAlignment.Center);
            _glyphs = _textLayout.GetTextData().OrderBy(g => g.Rect.X).ToArray();
            _textWidth = size.Width;
        }

        _lastShapedText = text;
        _lastShapedFontSize = FontSize;
    }

    // --- Caret / selection geometry ------------------------------------------------------------------------------

    // Pen X (text-local, before the scroll offset) of the caret sitting BEFORE character index. Glyphs are in text order.
    private double CaretOffset(int index)
    {
        EnsureLayout();
        if (_glyphs.Length == 0) return 0;
        if (index <= 0) return _glyphs[0].Rect.X;
        if (index >= _glyphs.Length) return _textWidth;
        return _glyphs[index].Rect.X;
    }

    private int IndexFromOffset(double x)
    {
        EnsureLayout();
        for (var i = 0; i < _glyphs.Length; i++)
        {
            var r = _glyphs[i].Rect;
            if (x < r.X + r.Width / 2.0) return i;
        }
        return _glyphs.Length;
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

    private void MoveCaretTo(int newIndex, bool extend)
    {
        newIndex = Clamp(newIndex);
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
    }

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

    /// <summary>Replace the selection (or insert at the caret when there is none) with <paramref name="insert"/>.</summary>
    protected void ReplaceSelection(string insert)
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

        Text = text.Substring(0, start) + insert + text.Substring(end);
        var newCaret = start + insert.Length;
        SelectionStart = newCaret;
        SelectionLength = 0;
        CaretIndex = newCaret;
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
        ReplaceSelection(filtered);
        e.Handled = true;
    }

    private void OnKeyDownHandler(object sender, KeyEventArgs e)
    {
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
            case Key.Home:
                MoveCaretTo(0, shift);
                e.Handled = true;
                break;
            case Key.End:
                MoveCaretTo(TextLength, shift);
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
        if (!string.IsNullOrEmpty(text)) ReplaceSelection(text.Replace("\r", "").Replace("\n", " "));
    }

    protected string SelectedText()
    {
        var (s, e) = SelectionRange();
        return (Text ?? string.Empty).Substring(s, e - s);
    }

    // --- Surface callbacks (the TextPresenter measures / renders / hit-tests through these) -----------------------

    internal Size MeasureSurface()
    {
        EnsureLayout();
        return new Size(_textWidth + CaretWidth, _lineHeight);   // + caret so an end-caret isn't clipped
    }

    internal void SurfaceMouseDown(double localX, bool extend)
    {
        Focus();
        MoveCaretTo(IndexFromOffset(localX + _scrollOffset), extend);
    }

    internal void SurfaceMouseMove(double localX)
        => MoveCaretTo(IndexFromOffset(localX + _scrollOffset), extend: true);

    internal void RenderSurface(IDrawingSession session, Size size)
    {
        EnsureLayout();
        EnsureCaretVisible(size.Width);

        var hasText = !string.IsNullOrEmpty(Text);
        var ox = -_scrollOffset;   // text-local -> surface: shift left by the scroll offset

        // Selection highlight (behind the text).
        if (HasSelection)
        {
            var (s, en) = SelectionRange();
            var x0 = ox + CaretOffset(s);
            var x1 = ox + CaretOffset(en);
            session.DrawRectangle(SelectionBrush ?? DefaultSelectionBrush, new Rect(x0, 0, Math.Max(0, x1 - x0), size.Height));
        }

        // Text, or the placeholder when empty and unfocused.
        if (hasText)
        {
            session.DrawText(BuildTextParameters(Foreground, ox, size), size, _textLayout, Foreground, Brushes.Transparent, Brushes.Transparent);
        }
        else if (!IsFocused && !string.IsNullOrEmpty(Placeholder))
        {
            RenderPlaceholder(session, size);
        }

        // Caret.
        if (IsFocused && _caretVisible)
        {
            var cx = ox + CaretOffset(CaretIndex);
            session.DrawRectangle(CaretBrush, new Rect(cx, 0, CaretWidth, size.Height));
        }
    }

    // Adjust the scroll offset so the caret stays inside the [0, width] viewport (WPF single-line behaviour: no scrollbar,
    // the text slides to follow the caret). Clamped so we never scroll past the text.
    private void EnsureCaretVisible(double viewportWidth)
    {
        if (viewportWidth <= 0) return;
        var caretX = CaretOffset(CaretIndex);
        if (caretX - _scrollOffset < 0) _scrollOffset = caretX;
        else if (caretX - _scrollOffset > viewportWidth - CaretPadding) _scrollOffset = caretX - viewportWidth + CaretPadding;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _textWidth + CaretWidth - viewportWidth));
    }

    private TextRenderingParameters BuildTextParameters(Brush color, double originX, Size size)
    {
        return new TextRenderingParameters
        {
            HorizontalTextAlignment = HorizontalTextAlignment.Left,
            VerticalTextAlignment = VerticalTextAlignment.Center,
            TextTrimming = TextTrimming.None,
            TextWrapping = TextWrapping.NoWrap,
            Color = (color as SolidColorBrush)?.Color ?? Colors.White,
            TextArea = new Rectangle(new Vector2F((float)originX, 0), size)
        };
    }

    private TextLayout _placeholderLayout;
    private string _placeholderShaped;
    private void RenderPlaceholder(IDrawingSession session, Size size)
    {
        var font = FontFamily ?? DefaultFontFamily;
        if (_placeholderLayout == null || !ReferenceEquals(_layoutFont, font)) _placeholderLayout = new TextLayout(font.Typeface, font.Fonts[0]);
        if (_placeholderShaped != Placeholder + "|" + FontSize)
        {
            _placeholderLayout.ProcessText(Placeholder, FontSize, new Size(double.PositiveInfinity, size.Height),
                TextWrapping.NoWrap, TextTrimming.None, HorizontalTextAlignment.Left, VerticalTextAlignment.Center);
            _placeholderShaped = Placeholder + "|" + FontSize;
        }
        session.DrawText(BuildTextParameters(PlaceholderForeground, 0, size), size, _placeholderLayout, PlaceholderForeground, Brushes.Transparent, Brushes.Transparent);
    }

    // --- Focus + caret blink -------------------------------------------------------------------------------------

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        StartBlink();
        InvalidateSurface();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _caretVisible = false;
        InvalidateSurface();
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
        tb.InvalidateSurface(measure: true);
    }

    private static void OnCaretOrSelectionChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => (a as TextBoxBase)?.InvalidateSurface();

    private static readonly Brush DefaultSelectionBrush = new SolidColorBrush(new Color(0x33, 0x99, 0xFF, 0x66));
}
