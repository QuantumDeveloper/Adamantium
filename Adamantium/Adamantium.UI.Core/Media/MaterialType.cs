namespace Adamantium.UI.Core.Media;

/// <summary>What a <see cref="MaterialBrush"/> is made of.
/// <para>The kinds differ along two independent axes, and only one of them is visible here. The SOURCE - what gets
/// captured from behind the element - is chosen by the material; the TREATMENT - what is done with that capture - is a
/// shader pass, and two materials can share it. Acrylic and LiquidGlass take the same capture and treat it differently;
/// Acrylic and Mica share a treatment and differ in what is handed to it.</para>
/// <para>A capture is NOT what makes something a material. A material answers "what is this made of"; only those whose
/// answer includes "you can see through it" need to look behind them at all. <see cref="Velvet"/> is the first that
/// does not: it depicts a SURFACE, and costs no capture whatsoever.</para></summary>
public enum MaterialType
{
    /// <summary>Frosted glass over what is DIRECTLY behind the element in this frame: blur, tint, grain. Follows the
    /// content beneath it - scroll something under an acrylic panel and the panel shows it moving.</summary>
    Acrylic,

    /// <summary>A tinted backdrop taken from the DESKTOP WALLPAPER behind the window, not from our own frame: blurred
    /// past recognition, so only broad colour survives. It does not react to the content at all - scrolling underneath
    /// leaves it still - and changes only when the window moves across the desktop or the wallpaper itself does.
    ///
    /// <para>Cheap for that same reason: the wallpaper is a FILE, decoded and blurred once (see
    /// <c>WallpaperBackdrop</c>), where acrylic pays a copy of the frame every time it draws. That is why mica can sit
    /// behind a whole window and acrylic is kept for panes.</para>
    ///
    /// <para>Where no wallpaper can be read - a plain-colour desktop, or a platform that does not expose one - it falls
    /// back to tinting that colour, which is a visible fallback rather than a silently disabled material.</para>
    ///
    /// <para>FROZEN WHILE THE WINDOW IS DRAGGED, and it rides along instead. Alone among the fills this one is anchored
    /// to the DESKTOP, so it needs the position the window will have when the frame is SHOWN - which the compositor
    /// decides afterwards. Measured: 7-24% of frames during a drag reach the screen already 8px out of date, peaks past
    /// 30, and that was the shaking. VSync does not fix it (it makes the frame wait, so the position in it is older
    /// still - a smooth slide instead of a shake); nothing inside the frame does, because 0.35ms is the whole path from
    /// reading the position to presenting. See RenderCache.WindowOnDesktop.</para></summary>
    Mica,

    /// <summary>A LENS, not frosting: the capture is sampled with an offset that grows towards the shape's edge, so what
    /// is behind bends the way it does through a thick drop of glass, with a chromatic fringe and a bright rim.
    /// Refraction at zero degrades gracefully into plain acrylic - the two are ends of one range.</summary>
    LiquidGlass,

    /// <summary>VELVET: a napped surface, and the first material that looks at nothing behind it. Its whole appearance
    /// is a light and a microscopic relief - fibres standing up off the cloth - so it costs no capture at all and is
    /// priced like a pattern rather than like glass.
    ///
    /// <para>What makes velvet read as velvet is that it is BRIGHTEST AT GRAZING ANGLES: the rim of a fold lights up
    /// while the face of it stays dark, which is the opposite of every ordinary surface. That is a sheen BRDF, and it
    /// is the same one glTF states as <c>KHR_materials_sheen</c>; suede, wool and felt are this material with a coarser
    /// nap and a harder highlight.</para>
    ///
    /// <para>The relief comes from a noise field: the noise is the height of the nap and its GRADIENT is the normal.
    /// A flat rectangle has one normal over its whole area, so without this any lighting model collapses into a flat
    /// fill - which is the failure to watch for.</para></summary>
    Velvet,

    /// <summary>METAL: the same lit surface as <see cref="Velvet"/> with the other half of the answer - a GGX lobe
    /// stretched along the grinding, and something to REFLECT. Steel, aluminium, chrome, gold, copper and brass are
    /// this one material: they differ in <see cref="MaterialBrush.MetalColor"/>, roughness and anisotropy, which is
    /// what actually separates them, rather than in six enumeration members that would each mean "the same shader with
    /// other numbers".
    ///
    /// <para>What it reflects is PROCEDURAL: behind a user interface there is no world, so capturing the frame would
    /// give a mirror of the window rather than of a room. A studio gradient - sky above the horizon, floor below - is
    /// cheap, controllable, and reads as metal. A capture-based mirror stays a separate material.</para></summary>
    Metal,

    /// <summary>WOOD: the third surface, and the one whose appearance is FIGURE rather than lighting. A board is a
    /// slice through a stack of concentric annual rings, so what shows on its face is where the cut plane crossed
    /// them - which is why timber shows arches and not stripes, and why the pattern is in the COLOUR while the light
    /// only varnishes it.
    ///
    /// <para>Two colours, not one: a ring is a broad pale band of spring growth ending in a narrow dense dark one, and
    /// the contrast between those two - see <see cref="MaterialBrush.EarlyWoodColor"/> and
    /// <see cref="MaterialBrush.LateWoodColor"/> - is what separates oak from walnut far more than any single "wood
    /// colour" could.</para></summary>
    Wood
}
