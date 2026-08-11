using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>The badge that says which key reaches a command while the ribbon is in key-tip mode. An adorner, so it draws
/// in the command's own space without entering anyone's tree, and it carries a template - what a key tip LOOKS like is
/// the theme's business, not this class's.</summary>
public class KeyTipAdorner : Adorner
{
    public static readonly AdamantiumProperty KeysProperty = AdamantiumProperty.Register(nameof(Keys),
        typeof(string), typeof(KeyTipAdorner), new PropertyMetadata(string.Empty, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>What to press. More than one character when the command needs it (Office's FN, ZA): the keys are typed
    /// in order and each one narrows what is still reachable.</summary>
    public string Keys
    {
        get => GetValue<string>(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    /// <summary>Where the badge sits against its command. Office does not put them all in one place: a tab header wears
    /// it below, a large command below its icon, a small one at the left edge.</summary>
    public static readonly AdamantiumProperty PlacementProperty = AdamantiumProperty.Register(nameof(Placement),
        typeof(KeyTipPlacement), typeof(KeyTipAdorner),
        new PropertyMetadata(KeyTipPlacement.Bottom, PropertyMetadataOptions.AffectsArrange));

    public KeyTipPlacement Placement
    {
        get => GetValue<KeyTipPlacement>(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public KeyTipAdorner(IUIComponent adornedElement, string keys, KeyTipPlacement placement) : base(adornedElement)
    {
        Keys = keys;
        Placement = placement;
    }

    // Sized to the badge, not to the command: this is a mark ON the target, not a frame AROUND it.
    public override bool FillsAdornedBounds => false;

    /// <summary>A badge hangs OFF its command (below a tab header, past the left edge of a small button), so the stage
    /// must be told the decoration reaches outside - or a strip that clips adorners would shave it.</summary>
    public override double ClipStandoff => 12;

    public override Rect PlaceIn(Size badge)
    {
        var target = AdornedBounds;

        var x = Placement switch
        {
            KeyTipPlacement.Left => target.X - badge.Width / 2,
            KeyTipPlacement.Right => target.Right - badge.Width / 2,
            _ => target.X + (target.Width - badge.Width) / 2,
        };

        var y = Placement switch
        {
            KeyTipPlacement.Top => target.Y - badge.Height / 2,
            KeyTipPlacement.Left or KeyTipPlacement.Right => target.Y + (target.Height - badge.Height) / 2,
            _ => target.Bottom - badge.Height / 2,
        };

        return new Rect(x, y, badge.Width, badge.Height);
    }
}
