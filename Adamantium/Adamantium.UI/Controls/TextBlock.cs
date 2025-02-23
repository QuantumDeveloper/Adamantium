using Adamantium.Graphics.Fonts;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;
using Adamantium.UI.Text;

namespace Adamantium.UI.Controls;

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
    
    public static readonly AdamantiumProperty FontFamilyProperty = AdamantiumProperty.Register(nameof(FontFamily),
        typeof(FontFamily), typeof(TextBlock),
        //new PropertyMetadata(new FontFamily(new Uri("J:\\AdamantiumProject\\Adamantium\\Adamantium.Game.Playground\\Fonts\\TTFFonts\\SourceSans3-Regular.ttf")), PropertyMetadataOptions.AffectsRender));
        //new PropertyMetadata(new FontFamily(new Uri("J:\\AdamantiumProject\\Adamantium\\Adamantium.Game.Playground\\Fonts\\OTFFonts\\Crimson-Italic.otf")), PropertyMetadataOptions.AffectsRender));
        new PropertyMetadata(new FontFamily("Cambria"), PropertyMetadataOptions.AffectsRender));
    
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

    public TextBlock()
    {
        _textLayout = new TextLayout(FontFamily.Typeface, FontFamily.Fonts[0]);
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

    public FontFamily FontFamily
    {
        get => GetValue<FontFamily>(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
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

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = _textLayout.ProcessText(Text, 
            FontSize, 
            new Size(Width, Height), 
            TextWrapping, 
            TextTrimming,
            HorizontalTextAlignment,
            VerticalTextAlignment);
        
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = _textLayout.ProcessText(Text, 
            FontSize, 
            new Size(Width, Height), 
            TextWrapping, 
            TextTrimming,
            HorizontalTextAlignment,
            VerticalTextAlignment);

        return size;
    }

    TextRenderingParameters GetTextRenderingParameters()
    {
        var textPos = new Vector2F();

        return new TextRenderingParameters()
        {
            HorizontalTextAlignment = HorizontalTextAlignment,
            VerticalTextAlignment = VerticalTextAlignment,
            TextTrimming = TextTrimming,
            TextWrapping = TextWrapping,
            Color = ((SolidColorBrush)Foreground).Color,
            TextArea = new Mathematics.Rectangle(textPos, DesiredSize)
        };
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawText(GetTextRenderingParameters(), DesiredSize, _textLayout, Background, Foreground);
    }
}