using System;
using System.IO;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A named picture the nine-slice stand can be dressed in. A type of its own so the drop-down has something to
/// show and something to hand back - the brush wants an ImageSource, the list wants a name.</summary>
public sealed class NineSliceSkin
{
    public NineSliceSkin(string name, string file)
    {
        Name = name;
        // Rooted against the app's base directory and taken through the cache, the same route the markup form takes:
        // a bare relative path resolves against the WORKING directory and silently fails to decode from an IDE.
        Source = BitmapImageCache.GetOrCreate(Path.Combine(AppContext.BaseDirectory, "Textures", file));
    }

    public string Name { get; }

    public BitmapImage Source { get; }

    public override string ToString() => Name;
}
