using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Media;

/// <summary>Fills a shape with a LIVE ELEMENT - a reflection, a thumbnail, a magnifier. WPF's <c>VisualBrush</c>, and
/// the one member of the family that cannot be replayed: a drawing is a recording and can simply be drawn again, but a
/// live subtree has layout, state and children of its own. So it is DRAWN OFF-SCREEN into a picture, and that picture
/// is what the fill samples - see the plan's §1.4, where this is the one case a bake is not a shortcut.
/// <para>The picture is re-made when the source says its content changed, which the element announces anyway
/// (<see cref="VisualTreeNotifications"/>); nothing polls it.</para></summary>
public sealed class VisualBrush : TileBrush
{
    // PAINT: the picture fills the shape it is given, so a different source re-colours the same pixels.
    public static readonly AdamantiumProperty VisualProperty = AdamantiumProperty.Register(nameof(Visual),
        typeof(IUIComponent), typeof(VisualBrush), new PropertyMetadata(null, PropertyMetadataOptions.AffectsPaint, OnVisualChanged));

    // The brush this one is a frozen clone OF, or null in the original. The render path reads a SNAPSHOT, and a fresh
    // snapshot is published on every property change - so the bake has to belong to the original, or each change would
    // key a new picture and the source would be drawn off-screen again for ever (the DrawingBrush lesson).
    private VisualBrush _origin;

    private BitmapSource _baked;

    public VisualBrush() { }

    public VisualBrush(IUIComponent visual) => Visual = visual;

    /// <summary>The element to paint with. It stays where it is - this draws it a second time, off-screen.</summary>
    public IUIComponent Visual
    {
        get => GetValue<IUIComponent>(VisualProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(VisualProperty, value);
        }
    }

    /// <summary>Where the state that survives a freeze lives: the picture, and whether it is stale.</summary>
    internal VisualBrush Origin => _origin ?? this;

    /// <summary>The last picture taken of the source, or null before the first one. Until then the fill draws nothing,
    /// the same answer a picture still being decoded gives.</summary>
    public override ImageSource ContentSource => Origin._baked;

    /// <summary>Stale: the source said its content changed (or it has never been drawn). Read and cleared by whoever
    /// takes the picture.</summary>
    internal bool NeedsBake { get; set; } = true;

    /// <summary>Take a fresh picture of the source at the next opportunity. Called for you when the source announces a
    /// change; public because a source can also change in ways it does not announce (a game surface, a video frame).</summary>
    public void Refresh()
    {
        var origin = Origin;
        origin.NeedsBake = true;
        origin.RaiseChanged();
    }

    /// <summary>Hand over the picture just taken. Raises <see cref="Brush.Changed"/>, so everything painting with this
    /// brush repaints - which is what makes a reflection follow its source.
    /// <para>The picture is NOT disposed here: every brush sharing a source is handed the SAME instance, so only
    /// whoever replaces it may free it.</para></summary>
    internal void Deliver(BitmapSource baked)
    {
        if (baked == null)
        {
            return;
        }

        var origin = Origin;
        origin._baked = baked;
        origin.RaiseChanged();
    }

    private static void OnVisualChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        // Only a DIFFERENT element on the ORIGINAL is worth a new picture. A binding re-pushes the same source whenever
        // the brush's expressions are refreshed (on attach, and again when the DataContext arrives), and a frozen CLONE
        // is handed the source at creation - which reads as null -> source. Either taken as a change costs an off-screen
        // render with its own render target, and the clone case is endless: delivering a picture publishes a snapshot,
        // which clones, which marks the original stale again.
        if (sender is VisualBrush brush && ReferenceEquals(brush.Origin, brush) && !ReferenceEquals(e.OldValue, e.NewValue))
        {
            brush.NeedsBake = true;
        }
    }

    protected override Brush CreateClone()
    {
        var clone = new VisualBrush { _origin = Origin, Visual = Visual };
        CopyTilingTo(clone);
        return clone;
    }
}
