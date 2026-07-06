using System.Collections.Generic;
using System.Collections.Specialized;
using Adamantium.Graphics.Fonts;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Text;

public class TextBlock : InputUIComponent
{
    public static readonly AdamantiumProperty TextProperty = AdamantiumProperty.Register(nameof(Text),
        typeof(string), typeof(TextBlock),
        new PropertyMetadata(string.Empty, PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsRender,
            TextChangedCallback));
    
    public static readonly AdamantiumProperty TextTrimmingProperty = AdamantiumProperty.Register(nameof(TextTrimming),
        typeof(TextTrimming), typeof(TextBlock),
        new PropertyMetadata(TextTrimming.None, PropertyMetadataOptions.AffectsRender, TextParametersChangedCallback));

    public static readonly AdamantiumProperty TextWrappingProperty = AdamantiumProperty.Register(nameof(TextWrapping),
        typeof(TextWrapping), typeof(TextBlock),
        new PropertyMetadata(TextWrapping.NoWrap, PropertyMetadataOptions.AffectsRender, TextParametersChangedCallback));
    
    public static readonly AdamantiumProperty HorizontalTextAlignmentProperty = AdamantiumProperty.Register(nameof(HorizontalTextAlignment),
        typeof(HorizontalTextAlignment), typeof(TextBlock),
        new PropertyMetadata(HorizontalTextAlignment.Left, PropertyMetadataOptions.AffectsRender, TextParametersChangedCallback));
    
    public static readonly AdamantiumProperty VerticalTextAlignmentProperty = AdamantiumProperty.Register(nameof(VerticalTextAlignment),
        typeof(VerticalTextAlignment), typeof(TextBlock),
        new PropertyMetadata(VerticalTextAlignment.Bottom, PropertyMetadataOptions.AffectsRender, TextParametersChangedCallback));

    public static readonly AdamantiumProperty JustifyLastLineProperty = AdamantiumProperty.Register(nameof(JustifyLastLine),
        typeof(bool), typeof(TextBlock),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender, TextParametersChangedCallback));
    
    // FontFamily is declared (inherited) on UIComponent. On a TextBlock a font change must re-shape the text, so override
    // the metadata with a callback that re-measures - this fires on BOTH a direct set and an inherited change cascaded
    // from an ancestor (RaiseInheritedChange invokes the callback; the AffectsMeasure flag would not fire on the cascade).
    static TextBlock()
    {
        FontFamilyProperty.OverrideMetadata(typeof(TextBlock),
            new PropertyMetadata(null, PropertyMetadataOptions.Inherits, OnFontFamilyChanged));
    }

    private static void OnFontFamilyChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        (a as TextBlock)?.InvalidateMeasure();
    }

    public static readonly AdamantiumProperty FontSizeProperty = AdamantiumProperty.Register(nameof(FontSize),
        typeof(double), typeof(TextBlock),
        new PropertyMetadata(12.0d, PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
        typeof (Brush), typeof (TextBlock),
        new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.BindsTwoWayByDefault|PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty ForegroundProperty = AdamantiumProperty.Register(nameof(Foreground),
        typeof (Brush), typeof (TextBlock),
        new PropertyMetadata(Brushes.White, PropertyMetadataOptions.BindsTwoWayByDefault|PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty StrokeProperty = AdamantiumProperty.Register(nameof(Stroke),
    typeof(Brush), typeof(TextBlock),
    new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender));

    private static void TextParametersChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        
    }

    private static void TextChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        
    }

    private TextLayout _textLayout;

    // Text shaping (ProcessText) is expensive, and measure+arrange both ran it EVERY layout pass - so a control whose
    // size animates (e.g. a Button growing, whose templated ContentPresenter re-measures this TextBlock each frame)
    // re-shaped the same unchanged text twice per frame. Cache the result and only re-shape when an input that actually
    // affects the layout changes.
    private Size _cachedSize;
    private bool _hasLayout;
    private FontFamily _layoutFont;   // the font _textLayout was built for (typeface is fixed per TextLayout)
    private string _lastText;
    private double _lastFontSize, _lastWidth, _lastHeight;
    // The width/height the last measure was given (a wrapping block reflows to this when it has no explicit Width, so
    // the wrap boundary comes from the container - like WPF - instead of a hardcoded Width). Infinity = unconstrained.
    private Size _lastConstraint = new Size(double.PositiveInfinity, double.PositiveInfinity);
    private TextWrapping _lastWrapping;
    private TextTrimming _lastTrimming;
    private HorizontalTextAlignment _lastHAlign;
    private VerticalTextAlignment _lastVAlign;
    private bool _lastJustify;

    public TextBlock()
    {
        Id = Guid.NewGuid().ToString();
        // _textLayout is built lazily in EnsureLayout: FontFamily is inherited and may be null here (it resolves against
        // an ancestor / DefaultFontFamily), and the typeface is fixed per TextLayout, so it's rebuilt when the font changes.
    }

    // Shapes the text only when an input that affects the layout changed since the last call; otherwise returns the
    // cached size (and leaves _textLayout as-is). Cheap to call every measure/arrange/render. Width/Height use
    // double.Equals so NaN==NaN counts as "unchanged" (an auto-sized label stays a cache hit while its parent resizes).
    private Size EnsureLayout()
    {
        // Resolve the inherited font (falling back to the single shared default), and (re)build the layout when it
        // changes - the typeface is fixed per TextLayout, so a font change means a new TextLayout for that face.
        var font = FontFamily ?? DefaultFontFamily;
        if (_textLayout == null || !ReferenceEquals(_layoutFont, font))
        {
            _textLayout = new TextLayout(font.Typeface, font.Fonts[0]);
            _layoutFont = font;
            _hasLayout = false;
        }

        // Wrap boundary: an explicit Width wins; otherwise, for a WRAPPING block, fall back to the width the container
        // gave this measure (WPF-style reflow to the parent) so wrapping needs no hardcoded Width. A NoWrap block keeps
        // the old unbounded behaviour (explicit Width only) so ordinary labels never start trimming to their slot. An
        // unconstrained parent (Infinity) stays unbounded. ProcessText maps NaN -> unbounded.
        var width = Width;
        if (double.IsNaN(width) && TextWrapping != TextWrapping.NoWrap && !double.IsInfinity(_lastConstraint.Width))
            width = _lastConstraint.Width;
        var height = Height;

        if (_hasLayout
            && _lastText == Text && _lastFontSize.Equals(FontSize)
            && _lastWidth.Equals(width) && _lastHeight.Equals(height)
            && _lastWrapping == TextWrapping && _lastTrimming == TextTrimming
            && _lastHAlign == HorizontalTextAlignment && _lastVAlign == VerticalTextAlignment
            && _lastJustify == JustifyLastLine)
        {
            return _cachedSize;
        }

        _cachedSize = _textLayout.ProcessText(Text, FontSize, new Size(width, height), TextWrapping, TextTrimming,
            HorizontalTextAlignment, VerticalTextAlignment, JustifyLastLine);

        _hasLayout = true;
        _lastText = Text; _lastFontSize = FontSize; _lastWidth = width; _lastHeight = height;
        _lastWrapping = TextWrapping; _lastTrimming = TextTrimming;
        _lastHAlign = HorizontalTextAlignment; _lastVAlign = VerticalTextAlignment; _lastJustify = JustifyLastLine;
        return _cachedSize;
    }

    public string Text
    {
        get => GetValue<string>(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    public TextTrimming TextTrimming
    {
        get => GetValue<TextTrimming>(TextTrimmingProperty);
        set => SetValue(TextTrimmingProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => GetValue<TextWrapping>(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }
    
    public HorizontalTextAlignment HorizontalTextAlignment
    {
        get => GetValue<HorizontalTextAlignment>(HorizontalTextAlignmentProperty);
        set => SetValue(HorizontalTextAlignmentProperty, value);
    }
    
    public VerticalTextAlignment VerticalTextAlignment
    {
        get => GetValue<VerticalTextAlignment>(VerticalTextAlignmentProperty);
        set => SetValue(VerticalTextAlignmentProperty, value);
    }

    /// <summary>
    /// When <see cref="HorizontalTextAlignment"/> is Justify, stretches the last (or only) line to
    /// the full width as well. Default is <c>false</c> - the last line stays ragged (text-align-last).
    /// </summary>
    public bool JustifyLastLine
    {
        get => GetValue<bool>(JustifyLastLineProperty);
        set => SetValue(JustifyLastLineProperty, value);
    }

    public double FontSize
    {
        get => GetValue<double>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }
    
    public Brush Background
    {
        get => GetValue<Brush>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }
    
    public Brush Foreground
    {
        get => GetValue<Brush>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Brush Stroke
    {
        get => GetValue<Brush>(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    // --- Bindable inline runs -----------------------------------------------------------------------------------------
    // When Inlines has content it REPLACES Text: the block renders each Run in sequence with its own (bindable) colour and
    // size. Each Run is a logical child (so it inherits this block's DataContext and its {Binding}s resolve), and this
    // block listens to every Run's Changed to re-shape when a bound value updates. Single line, no cross-run wrapping.

    private InlineCollection _inlines;
    private readonly List<InlinePiece> _pieces = [];
    private bool _piecesDirty = true;
    private Size _inlineSize;

    private sealed class InlinePiece
    {
        public TextLayout Layout;
        public double X;
        public Size Size;
        public Brush Foreground;
    }

    /// <summary>Bindable inline content. When non-empty it is rendered instead of <see cref="Text"/>: each <see cref="Run"/>
    /// carries its own bound Text/Foreground/FontSize.</summary>
    public InlineCollection Inlines
    {
        get
        {
            if (_inlines == null)
            {
                _inlines = [];
                _inlines.CollectionChanged += OnInlinesChanged;
            }
            return _inlines;
        }
    }

    private bool HasInlines => _inlines is { Count: > 0 };

    private void OnInlinesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (Inline old in e.OldItems) { old.Changed -= OnInlineChanged; RemoveLogicalChild(old); }
        if (e.NewItems != null)
            foreach (Inline added in e.NewItems) { AddLogicalChild(added); added.Changed += OnInlineChanged; }
        _piecesDirty = true;
        InvalidateMeasure();
    }

    private void OnInlineChanged(object sender, System.EventArgs e)
    {
        _piecesDirty = true;
        InvalidateMeasure();
    }

    // (Re)build the per-run layouts and their running X offsets. Cheap no-op unless a run/list/font changed.
    private Size EnsureInlineLayout()
    {
        if (!_piecesDirty) return _inlineSize;

        var font = FontFamily ?? DefaultFontFamily;
        _pieces.Clear();
        double x = 0, height = 0;
        foreach (var inline in _inlines)
        {
            if (inline is not Run run) continue;
            var text = run.Text ?? string.Empty;
            var fs = double.IsNaN(run.FontSize) ? FontSize : run.FontSize;
            var layout = new TextLayout(font.Typeface, font.Fonts[0]);
            var size = text.Length == 0
                ? Size.Zero
                : layout.ProcessText(text, fs, new Size(double.PositiveInfinity, double.PositiveInfinity),
                    TextWrapping.NoWrap, TextTrimming.None, HorizontalTextAlignment.Left, VerticalTextAlignment.Bottom);
            _pieces.Add(new InlinePiece { Layout = layout, X = x, Size = size, Foreground = run.Foreground ?? Foreground });
            x += size.Width;
            height = System.Math.Max(height, size.Height);
        }

        _inlineSize = new Size(x, height);
        _piecesDirty = false;
        return _inlineSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _lastConstraint = availableSize;   // a wrapping block reflows to this (its container's width) when it has no explicit Width
        return HasInlines ? EnsureInlineLayout() : EnsureLayout();
    }

    protected override Size ArrangeOverride(Size finalSize) => HasInlines ? EnsureInlineLayout() : EnsureLayout();

    TextRenderingParameters GetTextRenderingParameters()
    {
        var textPos = new Vector2F();

        return new TextRenderingParameters()
        {
            HorizontalTextAlignment = HorizontalTextAlignment,
            VerticalTextAlignment = VerticalTextAlignment,
            JustifyLastLine = JustifyLastLine,
            TextTrimming = TextTrimming,
            TextWrapping = TextWrapping,
            Color = ((SolidColorBrush)Foreground).Color,
            TextArea = new Rectangle(textPos, DesiredSize)
        };
    }

    protected override void OnRender(IDrawingContext context)
    {
        var session = context.ForControl(this);
        if (HasInlines)
        {
            EnsureInlineLayout();
            foreach (var piece in _pieces)
            {
                if (piece.Size.Width <= 0) continue;
                var pars = new TextRenderingParameters
                {
                    HorizontalTextAlignment = HorizontalTextAlignment.Left,
                    VerticalTextAlignment = VerticalTextAlignment.Bottom,
                    TextTrimming = TextTrimming.None,
                    TextWrapping = TextWrapping.NoWrap,
                    Color = (piece.Foreground as SolidColorBrush)?.Color ?? Colors.White,
                    TextArea = new Rectangle(new Vector2F((float)piece.X, 0), piece.Size)
                };
                session.DrawText(pars, piece.Size, piece.Layout, piece.Foreground, Brushes.Transparent, Brushes.Transparent);
            }
            return;
        }

        EnsureLayout();   // refresh shaping if a render-only property (alignment/wrapping) changed since the last measure
        session.DrawText(GetTextRenderingParameters(), DesiredSize, _textLayout, Foreground, Background, Stroke);
    }
}