using System;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.MVVM;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Media.Drawings;
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
        PushImageViewport();

        LiveDrawing.TileMode = _drawingTileMode;
        LiveDrawing.Stretch = _drawingStretch;
        LiveDrawing.ViewportUnits = _drawingViewportUnits;
        PushViewport();

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

    // The POLYGON every stand on this tab paints on, described by two numbers and shared by all of them: change the
    // corner count in one panel and every preview follows, which is the point - a brush is not tied to a shape, and the
    // same brush has to look right on a triangle, a hexagon and a near-circle.
    [Bindable] private double _shapeCorners = 3;
    [Bindable] private double _shapeStartAngle;

    /// <summary>The corner count the shapes actually take. A double above because sliders are doubles.</summary>
    public int ShapeCornerCount => (int)_shapeCorners;

    partial void OnShapeCornersChanged(double value) => RaisePropertyChanged(nameof(ShapeCornerCount));

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
    [Bindable] private double _imageRotation;
    [Bindable] private Color _imageTint = Colors.White;

    partial void OnImageTileModeChanged(TileMode value)
    {
        LiveImage.TileMode = value;
        PushImageViewport();
    }

    partial void OnImageStretchChanged(Stretch value) => LiveImage.Stretch = value;
    partial void OnImageRotationChanged(double value) => LiveImage.RotationAngle = value;
    partial void OnImageTintChanged(Color value) => LiveImage.Tint = value;

    partial void OnImageTileChanged(double value) => PushImageViewport();

    // One number for both axes: a square tile is what a texture is normally drawn as, and two sliders would be noise.
    // A single copy takes the WHOLE shape - that is what makes Stretch mean anything, and it is the pairing this stand
    // states above the sliders. Left at the tile size it would instead sit in a small corner of the shape.
    private void PushImageViewport()
    {
        var single = _imageTileMode == TileMode.None;
        LiveImage.ViewportUnits = single ? BrushMappingMode.RelativeToBoundingBox : BrushMappingMode.Absolute;
        LiveImage.Viewport = single ? new Rect(0, 0, 1, 1) : new Rect(0, 0, _imageTile, _imageTile);
    }
    partial void OnImageWidthChanged(double value)
    {
        ImageStar = Star(value, _imageHeight);
        RaisePropertyChanged(nameof(ImagePolygonSize));
    }

    partial void OnImageHeightChanged(double value)
    {
        ImageStar = Star(_imageWidth, value);
        RaisePropertyChanged(nameof(ImagePolygonSize));
    }

    /// <summary>The polygon takes ONE size for both axes - the smaller of the two, so it still fits the box the sliders
    /// set. Inscribed in an oblong box it is a perfectly good shape, but it reads as a regular one seen at an angle, and
    /// this stand is about the BRUSH: the shape must not be the thing you notice.</summary>
    public double ImagePolygonSize => Math.Min(_imageWidth, _imageHeight);

    /// <summary>The brush the "live drawing" rectangle fills with: a DRAWING rather than a picture, so the content has
    /// no pixels at all and every tile is replayed at the size it is drawn. Deliberately asymmetric, and authored in a
    /// 0..24 box with the shapes NOT filling it - so a viewbox that cuts a corner out of it is visibly a different
    /// picture, not just a crop of the same one.</summary>
    public DrawingBrush LiveDrawing { get; } = new DrawingBrush(new DrawingGroup
    {
        Children =
        {
            // SQUARE corners: a rounded backdrop leaves a notch at every tile joint, so the seam reads as a rendering
            // fault rather than as the tiling it is.
            new GeometryDrawing
            {
                Geometry = new RectangleGeometry { Rect = new Rect(0, 0, 24, 24) },
                Brush = new SolidColorBrush(new Color(30, 41, 59, 255))
            },
            new GeometryDrawing
            {
                Geometry = new EllipseGeometry { Center = new Vector2(8, 8), RadiusX = 5, RadiusY = 5 },
                Brush = new SolidColorBrush(new Color(56, 189, 248, 255))
            },
            new GeometryDrawing
            {
                Geometry = new RectangleGeometry { Rect = new Rect(14, 14, 8, 8) },
                Brush = new SolidColorBrush(new Color(244, 114, 182, 255))
            }
        }
    });

    // Drives a control INSIDE the VisualBrush's source, so dragging it changes the source and the fill has to follow -
    // which is the whole claim of the brush: it paints with a live element, not a picture of one taken once.
    [Bindable] private double _visualSourceValue = 60;

    // The brush itself is declared in MARKUP (its source is named there with ElementName - a view-model never holds a
    // UI element), so these drive it through bindings on the brush rather than by mutating an object here.
    [Bindable] private PreviewShape _visualShape = PreviewShape.Rectangle;
    [Bindable] private TileMode _visualTileMode = TileMode.None;
    [Bindable] private Stretch _visualStretch = Stretch.Uniform;
    [Bindable] private AlignmentX _visualAlignmentX = AlignmentX.Center;
    [Bindable] private AlignmentY _visualAlignmentY = AlignmentY.Center;
    [Bindable] private double _visualViewportSize = 120;
    [Bindable] private double _visualRotation;
    [Bindable] private double _visualOpacity = 1;
    [Bindable] private double _visualWidth = 260;
    [Bindable] private double _visualHeight = 170;
    [Bindable] private double _visualRadius = 12;
    [Bindable] private Color _visualTint = Colors.White;

    [Bindable] private PointsCollection _visualStar = Star(260, 170);

    partial void OnVisualWidthChanged(double value)
    {
        VisualStar = Star(value, _visualHeight);
        RaisePropertyChanged(nameof(VisualPolygonSize));
    }

    partial void OnVisualHeightChanged(double value)
    {
        VisualStar = Star(_visualWidth, value);
        RaisePropertyChanged(nameof(VisualPolygonSize));
    }

    /// <summary>The polygon takes ONE size for both axes - the smaller of the two, so it still fits the box the sliders
    /// set. Inscribed in an oblong box it is a perfectly good shape, but it reads as a regular one seen at an angle, and
    /// this stand is about the BRUSH: the shape must not be the thing you notice.</summary>
    public double VisualPolygonSize => Math.Min(_visualWidth, _visualHeight);

    public BrushMappingMode[] MappingModes { get; } = Enum.GetValues<BrushMappingMode>();

    public AlignmentX[] AlignmentsX { get; } = Enum.GetValues<AlignmentX>();

    public AlignmentY[] AlignmentsY { get; } = Enum.GetValues<AlignmentY>();

    [Bindable] private PreviewShape _drawingShape = PreviewShape.Rectangle;
    [Bindable] private TileMode _drawingTileMode = TileMode.Tile;
    [Bindable] private Stretch _drawingStretch = Stretch.Uniform;
    [Bindable] private AlignmentX _drawingAlignmentX = AlignmentX.Center;
    [Bindable] private AlignmentY _drawingAlignmentY = AlignmentY.Center;
    [Bindable] private BrushMappingMode _drawingViewportUnits = BrushMappingMode.Absolute;
    [Bindable] private double _drawingViewportSize = 72;
    [Bindable] private double _drawingViewportOriginX;
    [Bindable] private double _drawingViewportOriginY;
    [Bindable] private double _drawingViewboxX;
    [Bindable] private double _drawingViewboxY;
    [Bindable] private double _drawingViewboxSize = 1;
    [Bindable] private double _drawingWidth = 320;
    [Bindable] private double _drawingHeight = 200;
    [Bindable] private double _drawingRadius = 12;
    [Bindable] private double _drawingRotation;
    [Bindable] private double _drawingOpacity = 1;
    [Bindable] private Color _drawingTint = Colors.White;

    partial void OnDrawingTileModeChanged(TileMode value) => LiveDrawing.TileMode = value;
    partial void OnDrawingStretchChanged(Stretch value) => LiveDrawing.Stretch = value;
    partial void OnDrawingAlignmentXChanged(AlignmentX value) => LiveDrawing.AlignmentX = value;
    partial void OnDrawingAlignmentYChanged(AlignmentY value) => LiveDrawing.AlignmentY = value;
    partial void OnDrawingTintChanged(Color value) => LiveDrawing.Tint = value;
    partial void OnDrawingRotationChanged(double value) => LiveDrawing.RotationAngle = value;
    partial void OnDrawingOpacityChanged(double value) => LiveDrawing.Opacity = value;

    partial void OnDrawingViewportUnitsChanged(BrushMappingMode value)
    {
        LiveDrawing.ViewportUnits = value;
        // The two units mean different NUMBERS, so the slider is re-read into whichever is now in force rather than
        // carrying 72 (px) straight over into a relative viewport, which would be 72 shapes wide.
        PushViewport();
    }

    partial void OnDrawingViewportSizeChanged(double value) => PushViewport();
    partial void OnDrawingViewportOriginXChanged(double value) => PushViewport();
    partial void OnDrawingViewportOriginYChanged(double value) => PushViewport();

    partial void OnDrawingViewboxXChanged(double value) => PushViewbox();
    partial void OnDrawingViewboxYChanged(double value) => PushViewbox();
    partial void OnDrawingViewboxSizeChanged(double value) => PushViewbox();

    partial void OnDrawingWidthChanged(double value)
    {
        DrawingStar = Star(value, _drawingHeight);
        RaisePropertyChanged(nameof(DrawingPolygonSize));
    }

    partial void OnDrawingHeightChanged(double value)
    {
        DrawingStar = Star(_drawingWidth, value);
        RaisePropertyChanged(nameof(DrawingPolygonSize));
    }

    /// <summary>The polygon takes ONE size for both axes - the smaller of the two, so it still fits the box the sliders
    /// set. Inscribed in an oblong box it is a perfectly good shape, but it reads as a regular one seen at an angle, and
    /// this stand is about the BRUSH: the shape must not be the thing you notice.</summary>
    public double DrawingPolygonSize => Math.Min(_drawingWidth, _drawingHeight);

    // One slider for a SQUARE tile: two would be noise, and the point of the stand is which mechanism does what.
    // The ORIGIN converts with the size: the sliders are read in px, and handing a px origin to a RELATIVE viewport
    // means that many SHAPES across - the tile lands far outside and the fill simply disappears.
    private void PushViewport()
    {
        var scale = _drawingViewportUnits == BrushMappingMode.Absolute ? 1.0 : 1.0 / 200.0;
        LiveDrawing.Viewport = new Rect(
            _drawingViewportOriginX * scale,
            _drawingViewportOriginY * scale,
            _drawingViewportSize * scale,
            _drawingViewportSize * scale);
    }

    private void PushViewbox() =>
        LiveDrawing.Viewbox = new Rect(_drawingViewboxX, _drawingViewboxY, _drawingViewboxSize, _drawingViewboxSize);

    [Bindable] private PointsCollection _drawingStar = Star(320, 200);

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
        new NineSliceSkin("Gilded parchment", "nine-slice-parchment.png"),
        // ...and one that is NOT a picture: a drawing, rasterised to order before it is cut. The brush stays raster, so
        // what this one is for is checking that the raster it gets is made at the resolution the FRAME needs.
        NineSliceSkin.Vector()
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

    /// <summary>The figures a live stand can paint its brush onto - each stand picks its own, so two brushes can be
    /// compared on different shapes side by side. The rectangle and the ellipse go through their own SDF batches while
    /// the triangle is tessellated geometry, so the choice is also which render path the brush is riding.</summary>
    public PreviewShape[] PreviewShapes { get; } = Enum.GetValues<PreviewShape>();

    [Bindable] private PreviewShape _patternShape = PreviewShape.Rectangle;
    [Bindable] private PreviewShape _noiseShape = PreviewShape.Rectangle;
    [Bindable] private PreviewShape _meshShape = PreviewShape.Rectangle;
    [Bindable] private PreviewShape _imageShape = PreviewShape.Rectangle;

    // --- Backdrop material stand ---------------------------------------------------------------------------------
    // ONE live brush the controls drive in place, exactly as the aura below: the element holds this object and each
    // property change raises its own Changed, so the pane re-records without the stand rebuilding anything.
    //
    // The point of the stand is that acrylic and liquid glass are the SAME material with a different treatment - the
    // dropdown turns one into the other while everything else stays put, which is far more convincing than two static
    // swatches side by side. Mica is in the same list because it differs in the third way: not the treatment but the
    // source, the desktop instead of the frame.
    public MaterialBrush LiveMaterial { get; } = new MaterialBrush
    {
        Material = MaterialType.Acrylic,
        TintColor = new Color(32, 36, 46, 255),
        TintOpacity = 0.55,
        BlurAmount = 10,
        NoiseAmount = 0.04,
        Refraction = 18
    };

    public MaterialType[] MaterialTypes { get; } = Enum.GetValues<MaterialType>();

    [Bindable] private MaterialType _materialKind = MaterialType.Acrylic;
    [Bindable] private PreviewShape _materialShape = PreviewShape.Rectangle;
    [Bindable] private double _materialRadius = 24;
    [Bindable] private Color _materialTint = new Color(32, 36, 46, 255);
    [Bindable] private double _materialTintOpacity = 0.55;
    [Bindable] private double _materialBlur = 10;
    [Bindable] private double _materialGrain = 0.04;
    [Bindable] private double _materialRefraction = 18;
    [Bindable] private double _materialStroke = 0;
    [Bindable] private double _materialOpacity = 1.0;

    /// <summary>What mica may read, as ONE list - "built-in or mine" and "which of mine" are the same question asked
    /// twice. Chosen to differ in DETAIL: tile-sample is 64x64 across the whole desktop, texture.jpg is 1920x1200.
    /// </summary>
    public string[] MaterialSources { get; } =
    {
        DesktopWallpaperSource, "tile-sample.png", "elephant.png", "ColoredImage.jpg", "texture2.jpg", "texture.jpg"
    };

    private const string DesktopWallpaperSource = "Desktop wallpaper";

    public MaterialAnchor[] MaterialAnchors { get; } = Enum.GetValues<MaterialAnchor>();

    [Bindable] private string _materialSource = DesktopWallpaperSource;
    [Bindable] private MaterialAnchor _materialAnchor = MaterialAnchor.Desktop;

    /// <summary>Whether the anchor means anything right now: only mica takes a picture, and only a picture has to be
    /// pinned to anything.</summary>
    public bool HasOwnSource => _materialSource != DesktopWallpaperSource && _materialKind == MaterialType.Mica;

    partial void OnMaterialKindChanged(MaterialType value)
    {
        LiveMaterial.Material = value;
        RaisePropertyChanged(nameof(HasOwnSource));
    }

    // Loaded HERE, when it is picked, rather than up front: the whole set would otherwise be decoded on every start of
    // the tab for the one picture anybody looks at. The cache keeps what has already been asked for.
    partial void OnMaterialSourceChanged(string value)
    {
        LiveMaterial.Source = value == DesktopWallpaperSource
            ? null
            : BitmapImageCache.GetOrCreate(System.IO.Path.Combine(AppContext.BaseDirectory, "Textures", value));
        RaisePropertyChanged(nameof(HasOwnSource));
    }

    partial void OnMaterialAnchorChanged(MaterialAnchor value) => LiveMaterial.Anchor = value;

    partial void OnMaterialTintChanged(Color value) => LiveMaterial.TintColor = value;
    partial void OnMaterialTintOpacityChanged(double value) => LiveMaterial.TintOpacity = value;
    partial void OnMaterialBlurChanged(double value) => LiveMaterial.BlurAmount = value;
    partial void OnMaterialGrainChanged(double value) => LiveMaterial.NoiseAmount = value;
    partial void OnMaterialRefractionChanged(double value) => LiveMaterial.Refraction = value;

    /// <summary>What lies UNDER the material - the whole point of a backdrop is that it has something to show, and a
    /// flat colour proves nothing. Switchable because different fields expose different faults: a moving noise shows
    /// whether the capture is fresh, a hard-edged checkerboard shows how far the refraction bends, and a gradient shows
    /// banding the grain is there to hide.</summary>
    public MaterialUnderlay[] MaterialUnderlays { get; } = Enum.GetValues<MaterialUnderlay>();

    [Bindable] private MaterialUnderlay _materialUnderlay = MaterialUnderlay.LivingNoise;

    /// <summary>The photograph the material can be put over - the case it is actually used in, and the only one where
    /// "does this read as glass" can honestly be judged. The same sample the ImageBrush stand uses, through the same
    /// cache, so the tab loads it once.</summary>
    public ImageBrush MaterialPicture { get; } = new ImageBrush
    {
        Source = BitmapImageCache.GetOrCreate(
            System.IO.Path.Combine(AppContext.BaseDirectory, "Textures", "tile-sample.png")),
        Stretch = Stretch.UniformToFill
    };

    // --- Aura / Shadow stand -------------------------------------------------------------------------------------
    // Two live objects the sliders drive in place: the element holds THESE, and each property change raises their
    // Changed, so the band re-records without the stand rebuilding anything.
    public Aura LiveAura { get; } = new Aura
    {
        Radius = 28,
        Spread = 0,
        Color = new Color(56, 189, 248, 255),
        Opacity = 0.9
    };

    public Shadow LiveShadow { get; } = new Shadow
    {
        OffsetY = 10,
        BlurRadius = 18,
        Spread = 0,
        Color = new Color(0, 0, 0, 255),
        Opacity = 0.55
    };

    [Bindable] private bool _auraOn = true;
    [Bindable] private double _auraRadius = 28;
    [Bindable] private double _auraSpread;
    [Bindable] private Color _auraColor = new Color(56, 189, 248, 255);
    [Bindable] private double _auraOpacity = 0.9;
    [Bindable] private bool _auraInner;
    [Bindable] private double _auraTurbulence;
    [Bindable] private double _auraFlow = 0.5;
    [Bindable] private double _auraDetail = 3;
    [Bindable] private bool _auraPalette;
    [Bindable] private Color _auraColor1 = new Color(56, 189, 248, 255);
    [Bindable] private Color _auraColor2 = new Color(167, 139, 250, 255);
    [Bindable] private Color _auraColor3 = new Color(244, 114, 182, 255);
    [Bindable] private Color _auraColor4 = new Color(74, 222, 128, 255);

    [Bindable] private bool _shadowOn = true;
    [Bindable] private double _shadowOffsetX;
    [Bindable] private double _shadowOffsetY = 10;
    [Bindable] private double _shadowBlur = 18;
    [Bindable] private double _shadowSpread;
    [Bindable] private Color _shadowColor = Colors.Black;
    [Bindable] private double _shadowOpacity = 0.55;
    [Bindable] private bool _shadowInner;

    // The band is drawn OUTSIDE the element and the engine does not grow the layout for it - that is the author's job.
    // The stand makes the point live: drag the margin down and watch the glow get cut off by the panel.
    /// <summary>Every shape: the rect and the ellipse compute their distance, the polygon and the star READ one baked
    /// per mesh - same pass, same falloff, so there is nothing to leave out.</summary>
    public PreviewShape[] HaloShapes { get; } = Enum.GetValues<PreviewShape>();

    /// <summary>The halo stand's star. Its box is 180x120 rather than the size sliders of the image stand, so it is
    /// fixed - what moves here is the BAND, not the shape.</summary>
    public PointsCollection HaloStar { get; } = Star(180, 120);

    // Off by default: with it on and a small margin the stand looks broken rather than instructive. Ticking it is the
    // point - it shows what ANY clipping ancestor does to a band the author left no room for.
    [Bindable] private bool _haloClip;
    [Bindable] private double _haloMargin = 40;
    [Bindable] private double _haloRadius = 16;
    [Bindable] private PreviewShape _haloShape = PreviewShape.Rectangle;

    partial void OnAuraOnChanged(bool value) => LiveAura.IsEnabled = value;
    partial void OnShadowOnChanged(bool value) => LiveShadow.IsEnabled = value;
    partial void OnAuraRadiusChanged(double value) => LiveAura.Radius = value;
    partial void OnAuraSpreadChanged(double value) => LiveAura.Spread = value;
    partial void OnAuraColorChanged(Color value) => LiveAura.Color = value;
    partial void OnAuraOpacityChanged(double value) => LiveAura.Opacity = value;
    partial void OnAuraInnerChanged(bool value) => LiveAura.Inner = value;

    // Living: at Turbulence 0 this is exactly the cheap still band and the living pass is never reached. The palette is
    // authored as gradient stops so it goes through the SAME packer every gradient uses - a second way to pack a ramp is
    // how the hatch angle once went missing from one of two copies of the pattern record.
    partial void OnAuraTurbulenceChanged(double value) => LiveAura.Turbulence = value;
    partial void OnAuraFlowChanged(double value) => LiveAura.Flow = value;
    partial void OnAuraDetailChanged(double value) => LiveAura.Detail = value;

    partial void OnAuraPaletteChanged(bool value) => RebuildPalette();
    partial void OnAuraColor1Changed(Color value) => RebuildPalette();
    partial void OnAuraColor2Changed(Color value) => RebuildPalette();
    partial void OnAuraColor3Changed(Color value) => RebuildPalette();
    partial void OnAuraColor4Changed(Color value) => RebuildPalette();

    // A FRESH collection each time rather than editing the one the aura holds: the aura re-records on its own property
    // change, and mutating the collection in place would slip past that.
    private void RebuildPalette()
    {
        LiveAura.Palette = _auraPalette
            ? new GradientStopCollection
            {
                new GradientStop(_auraColor1, 0.0),
                new GradientStop(_auraColor2, 0.34),
                new GradientStop(_auraColor3, 0.67),
                new GradientStop(_auraColor4, 1.0)
            }
            : null;
    }

    partial void OnShadowOffsetXChanged(double value) => LiveShadow.OffsetX = value;
    partial void OnShadowOffsetYChanged(double value) => LiveShadow.OffsetY = value;
    partial void OnShadowBlurChanged(double value) => LiveShadow.BlurRadius = value;
    partial void OnShadowSpreadChanged(double value) => LiveShadow.Spread = value;
    partial void OnShadowColorChanged(Color value) => LiveShadow.Color = value;
    partial void OnShadowOpacityChanged(double value) => LiveShadow.Opacity = value;
    partial void OnShadowInnerChanged(bool value) => LiveShadow.Inner = value;

    /// <summary>The five-pointed star every stand can wear: the concave one of the four, so a fill has to survive
    /// reflex corners and a tessellation that is nothing like a quad.</summary>
    public PointsCollection FixedStar { get; } = Star(440, 300);

    /// <summary>The image stand's star, sized by the same Width/Height sliders the other figures follow - a Polygon
    /// holds authored coordinates rather than stretching to a slot, so the points are computed here.</summary>
    [Bindable] private PointsCollection _imageStar = Star(320, 200);

    // Ten points on a circle, alternating the full radius and the 0.382 of it that makes a star read as one; first point
    // straight up, so it stands the way a star is drawn rather than resting on a vertex.
    //
    // ONE radius for both axes - scaling x and y apart turns a star into a splat - and the result is then fitted into the
    // box and shifted so its bounding box starts at the ORIGIN. That last part is not cosmetic: a Shape measures from its
    // origin to its far edge, so a leading gap would be baked into the element's size and the figure would DRIFT as the
    // box is dragged instead of scaling with it.
    private static PointsCollection Star(double width, double height)
    {
        const double innerRatio = 0.382;
        var unit = new Vector2[10];
        var min = new Vector2(double.MaxValue, double.MaxValue);
        var max = new Vector2(double.MinValue, double.MinValue);
        for (var i = 0; i < unit.Length; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var radius = i % 2 == 0 ? 1.0 : innerRatio;
            unit[i] = new Vector2(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            min = Vector2.Min(min, unit[i]);
            max = Vector2.Max(max, unit[i]);
        }

        var scale = Math.Min(width / (max.X - min.X), height / (max.Y - min.Y));
        var points = new Vector2[unit.Length];
        for (var i = 0; i < unit.Length; i++)
        {
            points[i] = new Vector2((unit[i].X - min.X) * scale, (unit[i].Y - min.Y) * scale);
        }
        return new PointsCollection(points);
    }

    /// <summary>Every pattern kind, straight from the enum - the source for the "Pattern type" dropdown. It has no holes
    /// to skip any more: noise codes used to be punched into this enum's numbering and now live in their own range,
    /// so a kind added there shows up here by itself.</summary>
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
    }

    partial void OnFractalMorphSpeedChanged(double value)
    {
        LiveFractal.MorphSpeed = value;
    }

    partial void OnFractalCxChanged(double value)
    {
        LiveFractal.C = new Vector2((float)value, (float)_fractalCy);
    }

    partial void OnFractalCyChanged(double value)
    {
        LiveFractal.C = new Vector2((float)_fractalCx, (float)value);
    }

    partial void OnFractalCenterXChanged(double value) => ApplyCenter();   // base centre, driven by mouse pan/zoom (FractalView)
    partial void OnFractalCenterYChanged(double value) => ApplyCenter();

    partial void OnFractalFineXChanged(double value)
    {
        ApplyCenter();
    }

    partial void OnFractalFineYChanged(double value)
    {
        ApplyCenter();
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
}
