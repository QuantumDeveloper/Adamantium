using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Puts a TEXTURE on the plane: drag out where it goes, or click for a square.
/// <para>What is put down is a plain surface, and the picture is a BACKGROUND on it - which is all a texture is. The
/// file is chosen afterwards, on the surface's own Source row in the panel, so the gesture that places it is never
/// interrupted by a dialog and a texture can be moved, sized, turned and rounded before anyone has decided what it
/// shows.</para>
/// <para>Nothing of its own beyond the placing: a background, an outline and corners are what every control here
/// already has, so a texture is inspected with the same rows as a button is.</para></summary>
public class TextureTool : ElementTool
{
    public TextureTool() : base(Surface, new Size(200, 200))
    {
        Name = "Texture";
        Icon = "ToolTextureIcon";
        Description = "place a surface to paint a picture on";
        Shortcut = Key.I;

        // A BUTTON OF ITS OWN in the rail: what it puts down is the engine's, unlike the controls an application
        // offers, so it does not belong in their family.
        Group = string.Empty;
    }

    // An IMAGE, which is what a texture is even before it has a file: it carries the picture, its fitting and its
    // corners already. With nothing named yet it would draw nothing at all, so it is placed with a GROUND - and the
    // ground is what the panel then paints the chosen picture onto.
    private static IUIComponent Surface() => new Image
    {
        Background = CanvasTexture.Plain(),

        // UNDER the picture as well: the ground is where the texture is painted, so it must not get out of the way
        // when a file is named - that would leave the surface empty again, since nothing is in Source.
        BackgroundState = ImageBackgroundState.Always,

        // FILL, because the box a hand dragged out is the box it meant.
        Stretch = Stretch.Fill
    };
}
