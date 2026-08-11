namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One choice in the Materials gallery, as DATA: a gallery is shown twice at once - in the band and in its
/// drop-down - and a control can only be in one place.</summary>
public class MaterialSwatch
{
    public string Name { get; set; }

    /// <summary>The swatch colour, as a string the binding parses into a brush.</summary>
    public string Fill { get; set; }
}
