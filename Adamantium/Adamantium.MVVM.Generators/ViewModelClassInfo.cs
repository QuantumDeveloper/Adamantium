namespace Adamantium.MVVM.Generators;

/// <summary>Value-equatable model for a <c>[ViewModel]</c> class that needs the INPC implementation injected
/// (it doesn't already derive from a PropertyChangedBase-style base).</summary>
internal sealed record ViewModelClassInfo(
    string Namespace,
    string TypeKeyword,
    string TypeName,
    string HintName);
