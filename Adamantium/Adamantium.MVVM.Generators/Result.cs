namespace Adamantium.MVVM.Generators;

/// <summary>
/// A transform's outcome: either a <see cref="Value"/> to emit, or a <see cref="Diagnostic"/> to report (never both;
/// a transform returns <c>null</c> to skip entirely). Value-equatable (record over equatable parts), so it caches.
/// Lets every attribute share one diagnostic path without polluting each emit-model with a diagnostic field.
/// </summary>
internal sealed record Result<T>(T Value, DiagnosticInfo Diagnostic) where T : class;
