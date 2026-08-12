using System;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Brushes tab: a showcase of gradient (linear/radial/conic), pattern and noise brushes - authored in AUML - plus
/// a LIVE NoiseBrush whose FBM params are driven two-way by sliders, so dragging a slider re-bakes the procedural noise in
/// real time. Proves the brushes batch through the SDF batches and that a procedural brush re-renders on a param change.</summary>
[ViewModel]
public partial class BrushesViewModel : TabPageViewModel
{
    public BrushesViewModel() : base("Brushes")
    {
        LiveImage.TileMode = _imageTileMode;
        LiveImage.TileSize = new Size(_imageTile, _imageTile);

        // Built here rather than in an initializer: the brush's first skin comes from the list below, and one source of
        // truth for "which skin is on" beats repeating the path.
        _skin = Skins[0];
        LiveNineSlice = new NineSliceBrush
        {
            Source = _skin.Source,
            Slice = new Thickness(0.25),
            EdgeMode = NineSliceEdgeMode.Round
        };
    }

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

    /// <summary>The brush the "live pattern" rectangle fills with; the controls below drive its type/cell/colours - one
    /// configurable PatternBrush that replaces the static per-pattern swatch row.</summary>
    public PatternBrush LivePattern { get; } = new PatternBrush
    {
        Pattern = PatternType.Dots,
        CellSize = 22,
        Color1 = new Color(15, 23, 42, 255),
        Color2 = new Color(56, 189, 248, 255)
    };

    /// <summary>The brush the "live image" rectangle fills with: one picture, laid down once or repeated. The sample is
    /// deliberately ASYMMETRIC - a triangle, an off-centre dot and a corner square - so a plain tile shows its seams and
    /// a mirrored one visibly meets its own reflection.</summary>
    public ImageBrush LiveImage { get; } = new ImageBrush
    {
        Source = BitmapImageCache.GetOrCreate(
            System.IO.Path.Combine(AppContext.BaseDirectory, "Textures", "tile-sample.png"))
    };

    public TileMode[] TileModes { get; } = Enum.GetValues<TileMode>();

    /// <summary>Only the stretches that mean something for ONE copy; tiling ignores them.</summary>
    public Stretch[] Stretches { get; } = [Stretch.Fill, Stretch.Uniform, Stretch.UniformToFill, Stretch.None];

    [Bindable] private TileMode _imageTileMode = TileMode.Tile;
    [Bindable] private Stretch _imageStretch = Stretch.Fill;
    [Bindable] private double _imageTile = 64;
    [Bindable] private double _imageWidth = 320;
    [Bindable] private double _imageHeight = 200;
    [Bindable] private double _imageRadius;
    [Bindable] private Color _imageTint = Colors.White;

    partial void OnImageTileModeChanged(TileMode value) => LiveImage.TileMode = value;
    partial void OnImageStretchChanged(Stretch value) => LiveImage.Stretch = value;
    partial void OnImageTintChanged(Color value) => LiveImage.Tint = value;

    // One number for both axes: a square tile is what a texture is normally drawn as, and two sliders would be noise.
    partial void OnImageTileChanged(double value) => LiveImage.TileSize = new Size(value, value);

    /// <summary>The brush the "live mesh" rectangle fills with: four corner colours blended bilinearly, driven by four
    /// pickers. No axis and no stops - which is what makes it a different animal from the linear/radial family.</summary>
    public MeshGradientBrush LiveMesh { get; } = new MeshGradientBrush
    {
        TopLeft = new Color(14, 165, 233, 255),
        TopRight = new Color(168, 85, 247, 255),
        BottomLeft = new Color(34, 211, 238, 255),
        BottomRight = new Color(244, 114, 182, 255)
    };

    [Bindable] private Color _meshTopLeft = new Color(14, 165, 233, 255);
    [Bindable] private Color _meshTopRight = new Color(168, 85, 247, 255);
    [Bindable] private Color _meshBottomLeft = new Color(34, 211, 238, 255);
    [Bindable] private Color _meshBottomRight = new Color(244, 114, 182, 255);
    [Bindable] private double _meshRadius = 12;

    partial void OnMeshTopLeftChanged(Color value) => LiveMesh.TopLeft = value;
    partial void OnMeshTopRightChanged(Color value) => LiveMesh.TopRight = value;
    partial void OnMeshBottomLeftChanged(Color value) => LiveMesh.BottomLeft = value;
    partial void OnMeshBottomRightChanged(Color value) => LiveMesh.BottomRight = value;

    /// <summary>The brush the "live nine-slice" frame fills with. A skin resized live: dragging the frame's width and
    /// height shows what a nine-slice is FOR - the corners stay the size they were drawn at while everything between them
    /// gives.</summary>
    public NineSliceBrush LiveNineSlice { get; }

    /// <summary>The skins the stand can wear. All are 64x64 cut at 0.25, and all have HARD boundaries on the cut lines -
    /// a source anti-aliased there repeats its blurred row at every tile seam.</summary>
    public NineSliceSkin[] Skins { get; } =
    [
        new NineSliceSkin("Studded panel", "nine-slice-frame.png"),
        new NineSliceSkin("Stone blocks", "nine-slice-stone.png"),
        new NineSliceSkin("Sci-fi plating", "nine-slice-scifi.png"),
        new NineSliceSkin("Gilded parchment", "nine-slice-parchment.png")
    ];

    [Bindable] private NineSliceSkin _skin;

    partial void OnSkinChanged(NineSliceSkin value)
    {
        if (value != null) LiveNineSlice.Source = value.Source;
    }

    public NineSliceEdgeMode[] EdgeModes { get; } = Enum.GetValues<NineSliceEdgeMode>();

    // The frame's own size, so the skin can be stretched under the pointer - the whole point of a nine-slice.
    [Bindable] private double _sliceWidth = 320;
    [Bindable] private double _sliceHeight = 160;

    // The cut, as ONE fraction of the source on all four sides: what the demo's picture wants, and the number an author
    // actually thinks in. Per-side cuts exist on the brush; a slider each would be noise here.
    [Bindable] private double _sliceCut = 0.25;

    // How big the corners are DRAWN. 0 = the source's own pixels (1:1), which is the usual want; above that the skin
    // scales without touching the picture.
    [Bindable] private double _sliceBorder;

    [Bindable] private NineSliceEdgeMode _sliceEdgeMode = NineSliceEdgeMode.Round;
    [Bindable] private bool _sliceDrawCenter = true;
    [Bindable] private bool _sliceTileCenter;
    [Bindable] private Color _sliceTint = Colors.White;

    partial void OnSliceCutChanged(double value) => LiveNineSlice.Slice = new Thickness(value);
    partial void OnSliceBorderChanged(double value) => LiveNineSlice.Border = new Thickness(value);
    partial void OnSliceEdgeModeChanged(NineSliceEdgeMode value) => LiveNineSlice.EdgeMode = value;
    partial void OnSliceDrawCenterChanged(bool value) => LiveNineSlice.DrawCenter = value;
    partial void OnSliceTileCenterChanged(bool value) => LiveNineSlice.TileCenter = value;
    partial void OnSliceTintChanged(Color value) => LiveNineSlice.Tint = value;

    /// <summary>All procedural pattern types (Checkerboard..Hatch; the reserved noise slot has no name so GetValues skips
    /// it) - the source for the "Pattern type" dropdown.</summary>
    public PatternType[] PatternTypes { get; } = Enum.GetValues<PatternType>();

    [Bindable] private PatternType _patternKind = PatternType.Dots;
    [Bindable] private double _patternCell = 22;
    [Bindable] private double _patternHatchAngle = 45;   // Hatch line direction (deg); ignored by the other pattern types
    [Bindable] private Color _patternColor1 = new Color(15, 23, 42, 255);
    [Bindable] private Color _patternColor2 = new Color(56, 189, 248, 255);

    partial void OnPatternKindChanged(PatternType value) => LivePattern.Pattern = value;
    partial void OnPatternCellChanged(double value) => LivePattern.CellSize = value;
    partial void OnPatternHatchAngleChanged(double value) => LivePattern.HatchAngle = value;
    partial void OnPatternColor1Changed(Color value) => LivePattern.Color1 = value;
    partial void OnPatternColor2Changed(Color value) => LivePattern.Color2 = value;

    // Corner-radius sliders for the live tiles - bound to the Rectangle.CornerRadius via DoubleToCornerRadiusConverter, so
    // the view-model stays a plain double (no CornerRadius UI-primitive in the VM).
    [Bindable] private double _noiseRadius = 12;
    [Bindable] private double _patternRadius = 12;

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

    // Colour-picker bound: the two noise colours (low -> high). Match LiveNoise's initial colours so the pickers start right.
    [Bindable] private Color _noiseColor1 = new Color(11, 18, 32, 255);
    [Bindable] private Color _noiseColor2 = new Color(125, 211, 252, 255);

    partial void OnNoiseColor1Changed(Color value) => LiveNoise.Color1 = value;
    partial void OnNoiseColor2Changed(Color value) => LiveNoise.Color2 = value;

    /// <summary>All noise variants, in declaration order - the source for the "Noise type" dropdown.</summary>
    public NoiseType[] NoiseTypes { get; } = Enum.GetValues<NoiseType>();

    // Dropdown-bound noise-variant selector.
    [Bindable] private NoiseType _noiseKind;

    partial void OnNoiseKindChanged(NoiseType value) => LiveNoise.NoiseType = value;

    // Flow animation (best with NoiseType = Worley: the cells flow in place). Checkbox + speed slider.
    [Bindable] private bool _noiseAnimate;
    [Bindable] private double _noiseFlowSpeed = 1.0;

    partial void OnNoiseAnimateChanged(bool value) => LiveNoise.Animate = value;
    partial void OnNoiseFlowSpeedChanged(double value) => LiveNoise.FlowSpeed = value;

    // CombustibleVoronoi only: fire palette vs the brush's own Color1/Mid/Color2 ramp (checkbox below the pickers).
    [Bindable] private bool _noiseFirePalette = true;
    partial void OnNoiseFirePaletteChanged(bool value) => LiveNoise.UseFirePalette = value;

    // Mid colour = the ONLY thing that separates "tritone" from plain noise: with it on, the SAME noise maps through a
    // 3-colour gradient-map ramp (Color1 -> Mid -> Color2) instead of the 2-colour duotone. Off (checkbox clear) sets the
    // brush's MidColor transparent, which the shader reads as "duotone". Same brush, same pattern - only the colour mapping.
    [Bindable] private bool _noiseUseMid;
    [Bindable] private Color _noiseMid = new Color(249, 115, 22, 255);   // a warm mid; applied only while UseMid is on

    partial void OnNoiseUseMidChanged(bool value) => LiveNoise.MidColor = value ? _noiseMid : new Color(0, 0, 0, 0);

    partial void OnNoiseMidChanged(Color value)
    {
        if (_noiseUseMid)
        {
            LiveNoise.MidColor = value;
        }
    }

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
