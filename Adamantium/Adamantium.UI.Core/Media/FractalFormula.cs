using System.ComponentModel.DataAnnotations;

namespace Adamantium.UI.Core.Media;

/// <summary>The iteration map a <see cref="FractalBrush"/> uses - orthogonal to the Julia/Mandelbrot C-mode
/// (<see cref="FractalType"/>). Quadratic is the classic z²+c; Burning Ship / Tricorn / Celtic / Multibrot are
/// escape-time variants of it; Newton is different in kind - it colours which root of z³=1 each point converges to.</summary>
public enum FractalFormula
{
    Quadratic,

    [Display(Name = "Burning Ship")]
    BurningShip,

    Tricorn,

    Celtic,

    Multibrot,

    Newton
}
