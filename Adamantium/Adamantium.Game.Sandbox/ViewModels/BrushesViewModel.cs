using System;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Brushes tab: a showcase of gradient (linear/radial/conic), pattern and noise brushes - authored in AUML - plus
/// a LIVE NoiseBrush whose FBM params are driven two-way by sliders, so dragging a slider re-bakes the procedural noise in
/// real time. Proves the brushes batch through the SDF batches and that a procedural brush re-renders on a param change.</summary>
[ViewModel]
public partial class BrushesViewModel : TabPageViewModel
{
    public BrushesViewModel() : base("Brushes") { }

    /// <summary>The brush the "live noise" rectangle fills with; the sliders below drive its FBM params. One instance -
    /// mutating a property fires the brush's AffectsPaint change, which re-bakes the noise where it is drawn.</summary>
    public NoiseBrush LiveNoise { get; } = new NoiseBrush
    {
        Scale = 48,
        Octaves = 5,
        Seed = 0,
        Lacunarity = 2,
        Gain = 0.5,
        Color1 = new Color(11, 18, 32, 255),
        Color2 = new Color(125, 211, 252, 255)
    };

    // Slider-bound (double) FBM params; each pushes into LiveNoise (Octaves cast to int). The brush's paint change re-bakes.
    [Bindable] private double _noiseScale = 48;
    [Bindable] private double _noiseOctaves = 5;
    [Bindable] private double _noiseSeed = 0;
    [Bindable] private double _noiseLacunarity = 2;
    [Bindable] private double _noiseGain = 0.5;

    partial void OnNoiseScaleChanged(double value) => LiveNoise.Scale = value;
    partial void OnNoiseOctavesChanged(double value) => LiveNoise.Octaves = (int)value;
    partial void OnNoiseSeedChanged(double value) => LiveNoise.Seed = value;
    partial void OnNoiseLacunarityChanged(double value) => LiveNoise.Lacunarity = value;
    partial void OnNoiseGainChanged(double value) => LiveNoise.Gain = value;

    /// <summary>The live fractal brush the demo panel fills with; every control below drives one of its parameters. Toggling
    /// Animate makes its C drift on its own each frame (the brush keeps the render loop presenting while it's on).</summary>
    public FractalBrush LiveFractal { get; } = new FractalBrush
    {
        Fractal = FractalType.Julia,
        C = new Vector2(-0.8f, 0.156f),
        Zoom = 1.1,
        Iterations = 160,
        MorphSpeed = 1.0,
        Color1 = new Color(11, 18, 43, 255),
        Color2 = new Color(34, 211, 238, 255)
    };

    /// <summary>All fractal formulas, in declaration order - the source for the Formula dropdown (its friendly [Display]
    /// names are rendered by the DropDown; the bound value stays the real enum).</summary>
    public FractalFormula[] FractalFormulas { get; } = Enum.GetValues<FractalFormula>();

    // Every modifiable fractal parameter, exposed for the panel. Bools drive the two checkboxes (auto-morph, Julia/Mandelbrot);
    // the doubles drive sliders (C and Center are Vector2, so each axis rebuilds the whole vector from both fields).
    [Bindable] private FractalFormula _formula;
    [Bindable] private bool _fractalAnimate;
    [Bindable] private bool _fractalMandelbrot;
    [Bindable] private double _fractalPower = 2;
    [Bindable] private double _fractalZoomExp = 0.0414;   // log10(1.1); the Zoom slider is logarithmic so a linear drag zooms multiplicatively
    [Bindable] private double _fractalIterations = 160;
    [Bindable] private double _fractalMorphSpeed = 1.0;
    [Bindable] private double _fractalCx = -0.8;
    [Bindable] private double _fractalCy = 0.156;
    [Bindable] private double _fractalCenterX = 0;
    [Bindable] private double _fractalCenterY = 0;
    [Bindable] private double _fractalFineX = 0;   // fine pan, scaled by 1/zoom in ApplyCenter so it stays precise when zoomed in
    [Bindable] private double _fractalFineY = 0;
    [Bindable] private Color _fractalColor1 = new Color(11, 18, 43, 255);
    [Bindable] private Color _fractalColor2 = new Color(34, 211, 238, 255);

    partial void OnFormulaChanged(FractalFormula value) => LiveFractal.Formula = value;
    partial void OnFractalAnimateChanged(bool value) => LiveFractal.Animate = value;
    partial void OnFractalMandelbrotChanged(bool value) => LiveFractal.Fractal = value ? FractalType.Mandelbrot : FractalType.Julia;
    partial void OnFractalPowerChanged(double value)
    {
        LiveFractal.Power = value;
        RaisePropertyChanged(nameof(PowerText));
    }

    partial void OnFractalZoomExpChanged(double value)
    {
        LiveFractal.Zoom = Math.Pow(10, value);   // slider holds log10(Zoom) - a linear drag zooms multiplicatively
        ApplyCenter();   // the fine-pan offset scales by 1/zoom, so re-apply the centre when zoom changes
        RaisePropertyChanged(nameof(ZoomText));
    }

    partial void OnFractalIterationsChanged(double value)
    {
        LiveFractal.Iterations = (int)value;
        RaisePropertyChanged(nameof(IterationsText));
    }

    partial void OnFractalMorphSpeedChanged(double value)
    {
        LiveFractal.MorphSpeed = value;
        RaisePropertyChanged(nameof(MorphSpeedText));
    }

    partial void OnFractalCxChanged(double value)
    {
        LiveFractal.C = new Vector2((float)value, (float)_fractalCy);
        RaisePropertyChanged(nameof(CxText));
    }

    partial void OnFractalCyChanged(double value)
    {
        LiveFractal.C = new Vector2((float)_fractalCx, (float)value);
        RaisePropertyChanged(nameof(CyText));
    }

    partial void OnFractalCenterXChanged(double value) => ApplyCenter();   // base centre, driven by mouse pan/zoom (FractalView)
    partial void OnFractalCenterYChanged(double value) => ApplyCenter();

    partial void OnFractalFineXChanged(double value)
    {
        ApplyCenter();
        RaisePropertyChanged(nameof(FineXText));
    }

    partial void OnFractalFineYChanged(double value)
    {
        ApplyCenter();
        RaisePropertyChanged(nameof(FineYText));
    }

    partial void OnFractalColor1Changed(Color value) => LiveFractal.Color1 = value;
    partial void OnFractalColor2Changed(Color value) => LiveFractal.Color2 = value;

    // Effective centre = coarse base + fine offset, the fine offset scaled by the viewport (1.5 / zoom) so axis panning
    // stays precise at any depth. Mouse pan/zoom writes the base CenterX/Y and ZoomExp (two-way from FractalView).
    private void ApplyCenter()
    {
        var span = 1.5 / Math.Max(Math.Pow(10, _fractalZoomExp), 1e-4);
        LiveFractal.Center = new Vector2(
            (float)(_fractalCenterX + _fractalFineX * span),
            (float)(_fractalCenterY + _fractalFineY * span));
    }

    // Read-outs to the right of each slider - the sliders emit continuous doubles, so format them to stay legible.
    public string ZoomText => $"{Math.Pow(10, FractalZoomExp):0.##}×";
    public string PowerText => FractalPower.ToString("0.##");
    public string IterationsText => ((int)FractalIterations).ToString();
    public string MorphSpeedText => FractalMorphSpeed.ToString("0.##");
    public string CxText => FractalCx.ToString("0.###");
    public string CyText => FractalCy.ToString("0.###");
    public string FineXText => FractalFineX.ToString("0.##");
    public string FineYText => FractalFineY.ToString("0.##");
}
