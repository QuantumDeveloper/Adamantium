using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Buttons;

public class Button : ContentControl
{
    public Button()
    {
        
    }
    
    public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
        typeof (Brush), typeof (Button),
        new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
        typeof (CornerRadius), typeof (Button),
        new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty BorderThicknessProperty =
        AdamantiumProperty.Register(nameof(BorderThickness),
            typeof (Thickness), typeof (Button),
            new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));
    
    public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
        typeof(Thickness), typeof(Button),
        new PropertyMetadata(default(Thickness),
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));
    
    public static readonly AdamantiumProperty CommandProperty = AdamantiumProperty.Register(nameof(Command),
        typeof(ICommand), typeof(Button),
        new PropertyMetadata(null));
    
    public static readonly AdamantiumProperty IsPressedProperty = AdamantiumProperty.Register(nameof(IsPressed),
        typeof(bool), typeof(Button), new PropertyMetadata(false));
    
    public static readonly RoutedEvent ClickEvent =
        EventManager.RegisterRoutedEvent( nameof(Click),
            RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Button));
    
    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }
    
    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public bool IsPressed
    {
        get => GetValue<bool>(IsPressedProperty);
        set => SetValue(IsPressedProperty, value);
    }

    public Brush BorderBrush
    {
        get => GetValue<Brush>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue<CornerRadius>(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }
    
    public ICommand Command
    {
        get => GetValue<ICommand>(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OnClick();
    }

    protected virtual void OnClick()
    {
        if (Command != null && Command.CanExecute())
        {
            Command.Execute();
        }
        
        var eventArgs = new RoutedEventArgs(ClickEvent, this)
        {
            RoutedEvent = ClickEvent
        };
        RaiseEvent(eventArgs);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
    }

    // protected override Size MeasureOverride(Size availableSize)
    // {
    //     var padding = Padding;
    //     double paddingWidth = padding.Left + padding.Right;
    //     double paddingHeight = padding.Top + padding.Bottom;
    //
    //     Size contentSize = Size.Zero;
    //
    //     if (Content is IMeasurableComponent content)
    //     {
    //         Size contentAvailable = new Size(
    //             Math.Max(0, availableSize.Width - paddingWidth),
    //             Math.Max(0, availableSize.Height - paddingHeight));
    //         content.Measure(contentAvailable);
    //         contentSize = content.DesiredSize;
    //     }
    //
    //     Size totalSize = new Size(
    //         contentSize.Width + paddingWidth,
    //         contentSize.Height + paddingHeight);
    //
    //     totalSize.Width = Math.Max(MinWidth, Math.Min(totalSize.Width, MaxWidth));
    //     totalSize.Height = Math.Max(MinHeight, Math.Min(totalSize.Height, MaxHeight));
    //     
    //     return base.MeasureOverride(availableSize);
    // }
    //
    //
    // protected override Size ArrangeOverride(Size finalSize)
    // {
    //     InvalidateRender(true);
    //     var padding = Padding;
    //     double paddingWidth = padding.Left + padding.Right;
    //     double paddingHeight = padding.Top + padding.Bottom;
    //
    //     if (Content is IMeasurableComponent content)
    //     {
    //         Rect contentRect = new Rect(
    //             padding.Left,
    //             padding.Top,
    //             Math.Max(0, finalSize.Width - paddingWidth),
    //             Math.Max(0, finalSize.Height - paddingHeight));
    //         content.Arrange(contentRect);
    //     }
    //
    //     return base.ArrangeOverride(finalSize);
    // }
}