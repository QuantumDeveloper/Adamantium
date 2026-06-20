using Microsoft.CodeAnalysis;

namespace Adamantium.MVVM.Generators;

/// <summary>
/// An incremental-safe diagnostic: a (cached, singleton) descriptor + an optional <see cref="LocationInfo"/> + one
/// message argument. Produced inside a transform and materialized to a <see cref="Diagnostic"/> in the source-output
/// step (where reporting is allowed). Value-equatable, so carrying it through the pipeline doesn't defeat caching.
/// </summary>
internal sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo Location, string MessageArg)
{
    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, MessageArg);
}
