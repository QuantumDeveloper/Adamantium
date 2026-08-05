using System;
using System.Collections.ObjectModel;
using Adamantium.Core;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Drives the Transforms tab: one rotation/shear/scale setting fans out over a grid of tiles, each tile offset
/// by its index so no two are drawn under the same matrix. The point is what the RENDERER does with that - every tile is
/// a rounded, stroked rect, i.e. SDF-batch content, and it stays batched however it is turned or sheared.</summary>
public sealed class TransformSettings : PropertyChangedBase
{
    private static readonly string[] Palette =
        ["#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#8B5CF6", "#14B8A6", "#EC4899", "#EAB308"];

    public TransformSettings()
    {
        for (var i = 0; i < 240; i++)
            Tiles.Add(new TransformTile { Color = Palette[i % Palette.Length] });
        Apply();
    }

    public ObservableCollection<TransformTile> Tiles { get; } = new();

    private double _rotation = 20;
    public double Rotation { get => _rotation; set { if (SetProperty(ref _rotation, value)) Apply(); } }

    private double _skewX = 12;
    public double SkewX { get => _skewX; set { if (SetProperty(ref _skewX, value)) Apply(); } }

    private double _skewY;
    public double SkewY { get => _skewY; set { if (SetProperty(ref _skewY, value)) Apply(); } }

    private double _scale = 1.0;
    public double Scale { get => _scale; set { if (SetProperty(ref _scale, value)) Apply(); } }

    // How much each tile's transform differs from its neighbour's. At 0 the whole grid shares one matrix; turned up, every
    // tile gets its own - which is the case that used to fall out of the batch, one draw call per tile.
    private double _spread = 1.5;
    public double Spread { get => _spread; set { if (SetProperty(ref _spread, value)) Apply(); } }

    private void Apply()
    {
        for (var i = 0; i < Tiles.Count; i++)
        {
            var tile = Tiles[i];
            tile.Angle = _rotation + i * _spread;
            tile.SkewX = _skewX * Math.Cos(i * 0.13);
            tile.SkewY = _skewY * Math.Sin(i * 0.11);
            tile.Scale = _scale;
        }
    }
}
