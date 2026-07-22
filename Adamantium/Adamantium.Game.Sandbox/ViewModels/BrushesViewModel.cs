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
}
