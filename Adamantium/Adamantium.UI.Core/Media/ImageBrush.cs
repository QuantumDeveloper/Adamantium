using Adamantium.Mathematics;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Media;

/// <summary>Fills a shape with an IMAGE - the first brush whose colour is SAMPLED rather than computed. All the
/// tiling, fitting and cropping lives on <see cref="TileBrush"/>; what this adds is only WHERE the picture comes
/// from.</summary>
public sealed class ImageBrush : TileBrush
{
    // PAINT: the image fills the shape it is given, so swapping it re-colours the same pixels and never touches layout.
    public static readonly AdamantiumProperty SourceProperty = AdamantiumProperty.Register(nameof(Source),
        typeof(ImageSource), typeof(ImageBrush), new PropertyMetadata(null, PropertyMetadataOptions.AffectsPaint, OnSourceChanged));

    public ImageBrush() { }

    public ImageBrush(ImageSource source) => Source = source;

    public ImageSource Source
    {
        get => GetValue<ImageSource>(SourceProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(SourceProperty, value);
        }
    }

    public override ImageSource ContentSource => Source;

    private static void OnSourceChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender is ImageBrush brush)
        {
            TexturedBrushSource.RepaintWhenLoaded(e.NewValue as ImageSource, brush.RaiseChanged);
        }
    }

    protected override Brush CreateClone()
    {
        var clone = new ImageBrush { Source = Source };
        CopyTilingTo(clone);
        return clone;
    }
}
