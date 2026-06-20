namespace Adamantium.MVVM.Generators;

/// <summary>Everything needed to emit a <c>[Bindable]</c> field's generated property — value-equatable (record +
/// <see cref="EquatableArray{T}"/>) so the generator caches it and only regenerates when this member changes.</summary>
internal sealed record BindableMemberInfo(
    string Namespace,
    string TypeKeyword,
    string TypeName,
    string FieldName,
    string PropertyName,
    string PropertyType,
    bool HasInpcBase,
    EquatableArray<string> AffectsProperties,
    EquatableArray<string> AffectsCommands,
    string HintName);
