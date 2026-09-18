using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>What a TEXTURE is made of, as the canvas puts one on the plane.
/// <para>A texture is not a control of its own and not a picture of its own: it is a surface whose BACKGROUND happens
/// to be a picture. Everything else about it - its box, its outline, its corners, its turn - is what every control on
/// the plane already has, and the file it shows is chosen on its Source row like any other background.</para></summary>
public static class CanvasTexture
{
    /// <summary>The fill a texture is PLACED with, before any file has been chosen: something to see and to take hold
    /// of. Not transparent - a thing that cannot be seen cannot be told from the plane it stands on, and the one thing
    /// a person does next is reach for it.</summary>
    public static Brush Plain() => new SolidColorBrush(new Color(96, 96, 96, 255));
}
