using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Adamantium.MVVM.Generators;

/// <summary>
/// An incremental-safe stand-in for <see cref="Location"/>: stores only value types (file path + spans), with no
/// reference to a SyntaxTree or Compilation, so a model carrying it still caches correctly. The real
/// <see cref="Location"/> is rebuilt at report time via <see cref="ToLocation"/>.
/// </summary>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo CreateFrom(ISymbol symbol) =>
        CreateFrom(symbol.Locations.FirstOrDefault(l => l.SourceTree is not null) ?? symbol.Locations.FirstOrDefault());

    public static LocationInfo CreateFrom(Location location)
    {
        if (location?.SourceTree is null) return null;
        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }
}
