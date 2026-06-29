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

        if (_hasLayout
            && _lastText == Text && _lastFontSize.Equals(FontSize)
            && _lastWidth.Equals(Width) && _lastHeight.Equals(Height)
            && _lastWrapping == TextWrapping && _lastTrimming == TextTrimming
            && _lastHAlign == HorizontalTextAlignment && _lastVAlign == VerticalTextAlignment
            && _lastJustify == JustifyLastLine)
        {
            return _cachedSize;
        }

        _cachedSize = _textLayout.ProcessText(Text, FontSize, new Size(Width, Height), TextWrapping, TextTrimming,
            HorizontalTextAlignment, VerticalTextAlignment, JustifyLastLine);

        _hasLayout = true;
        _lastText = Text; _lastFontSize = FontSize; _lastWidth = Width; _lastHeight = Height;
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

    protected override Size MeasureOverride(Size availableSize) => EnsureLayout();

    protected override Size ArrangeOverride(Size finalSize) => EnsureLayout();

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
        EnsureLayout();   // refresh shaping if a render-only property (alignment/wrapping) changed since the last measure
        context.ForControl(this).DrawText(GetTextRenderingParameters(), DesiredSize, _textLayout, Foreground, Background, Stroke);
    }
}