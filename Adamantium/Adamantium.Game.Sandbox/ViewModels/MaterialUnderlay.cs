namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>What is placed BEHIND the material on its stand. A backdrop material has nothing of its own to show, so
/// what it is shown over decides what the stand actually demonstrates - and each of these exposes a different property
/// of it.</summary>
public enum MaterialUnderlay
{
    /// <summary>Moving noise. Shows whether the capture is taken FRESH each frame: a stale one would sit still while
    /// the field beneath the pane keeps moving.</summary>
    LivingNoise,

    /// <summary>A hard checkerboard. Straight edges are the only honest way to see how far the glass BENDS what is
    /// behind it - on a soft field a lens and a blur look much the same.</summary>
    Checkerboard,

    /// <summary>A smooth gradient. Shows banding, which is exactly what the grain is there to hide: the capture came
    /// from an 8-bit target and was smoothed twice, so its gradients are flatter than the eye tolerates.</summary>
    Gradient,

    /// <summary>A photograph. The case the materials are actually used over, and the one where "does it read as glass"
    /// can be judged at all.</summary>
    Picture
}
