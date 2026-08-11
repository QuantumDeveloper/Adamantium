using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>One choice in a <see cref="RibbonGallery"/> - the item container the gallery generates. A ContentControl and
/// not a button: a gallery cell shows what the choice LOOKS like, and the click picks it rather than running it.</summary>
public class RibbonGalleryItem : ContentControl, ISelectable
{
    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(RibbonGalleryItem), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    // State brushes the theme's triggers project onto the chrome. Null = no change in that state.
    public static readonly AdamantiumProperty BackgroundPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundPointerOver), typeof(Brush), typeof(RibbonGalleryItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BackgroundSelectedProperty = AdamantiumProperty.Register(
        nameof(BackgroundSelected), typeof(Brush), typeof(RibbonGalleryItem), new PropertyMetadata(default(Brush)));

    public static readonly AdamantiumProperty BorderBrushSelectedProperty = AdamantiumProperty.Register(
        nameof(BorderBrushSelected), typeof(Brush), typeof(RibbonGalleryItem), new PropertyMetadata(default(Brush)));

    static RibbonGalleryItem()
    {
        FocusableProperty.OverrideMetadata(typeof(RibbonGalleryItem), new PropertyMetadata(true));
    }

    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public Brush BackgroundPointerOver
    {
        get => GetValue<Brush>(BackgroundPointerOverProperty);
        set => SetValue(BackgroundPointerOverProperty, value);
    }

    public Brush BackgroundSelected
    {
        get => GetValue<Brush>(BackgroundSelectedProperty);
        set => SetValue(BackgroundSelectedProperty, value);
    }

    public Brush BorderBrushSelected
    {
        get => GetValue<Brush>(BorderBrushSelectedProperty);
        set => SetValue(BorderBrushSelectedProperty, value);
    }

    /// <summary>Who this cell belongs to. TOLD, not walked up to: half of these cells live in the gallery's drop-down,
    /// and popup content is a detached subtree with no ancestors to find.</summary>
    internal RibbonGallery Owner { get; set; }

    /// <summary>The selection only reflects onto containers when it CHANGES, so a cell realized afterwards - the
    /// drop-down builds a second set of them - pulls the current state here.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Owner is { } owner) IsSelected = owner.IsItemSelectedFor(this);
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (!IsEnabled || e.Handled) return;

        e.Handled = true;
        Focus();
        Owner?.PickFromContainer(this);
    }

    /// <summary>Enter/Space picks the focused choice - a gallery walked into with the arrows must be usable without
    /// the mouse.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.OriginalSource != this) return;

        if (e.Key is not (Key.Enter or Key.Space)) return;

        Owner?.PickFromContainer(this);
        e.Handled = true;
    }
}
