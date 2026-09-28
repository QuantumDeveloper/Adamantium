namespace Adamantium.MVVM.Generators;

/// <summary>Value-equatable model for a <c>[ViewModel]</c> class that has no INPC host yet. When
/// <see cref="InheritBase"/> is true (a plain class with no other base) it is simply made to derive from the ready
/// AdamantiumViewModel; otherwise (a struct, or a class keeping a different base) the INPC members are injected.</summary>
internal sealed record ViewModelClassInfo(
    string Namespace,
    string TypeKeyword,
    string TypeName,
    bool InheritBase,
    string HintName);
