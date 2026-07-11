using System.Collections.Generic;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Tiles 3D tab: the WPF-classic flip-tile mosaic (the transform-table acceptance demo). The board mechanics
/// (tilt field, flip wave, shared photo + per-tile UV fragments) live in the TilesHost control - the view-model only
/// supplies the tiles' front colours and the flip-all switch.</summary>
[ViewModel]
public partial class TilesViewModel : TabPageViewModel
{
    public const int Columns = 12;
    public const int Rows = 7;

    private static readonly string[] Palette =
        ["#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#8B5CF6", "#14B8A6", "#EC4899", "#EAB308"];

    public IReadOnlyList<TileItem> Tiles { get; }

    [Bindable] private bool _allFlipped;

    [Command] private void FlipAll() => AllFlipped = !AllFlipped;

    public TilesViewModel() : base("Tiles 3D")
    {
        var tiles = new List<TileItem>(Columns * Rows);
        for (var row = 0; row < Rows; row++)
        for (var col = 0; col < Columns; col++)
        {
            tiles.Add(new TileItem(Palette[(row + col) % Palette.Length]));
        }
        Tiles = tiles;
    }
}

/// <summary>One board tile: just its front colour - geometry, photo fragment and wave timing come from the host.</summary>
public sealed class TileItem(string color)
{
    public string Color { get; } = color;
}
