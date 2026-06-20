namespace Adamantium.MVVM.Generators;

/// <summary>Everything needed to emit a <c>[Bindable]</c> member's generated property — value-equatable (record +
/// <see cref="EquatableArray{T}"/>) so the generator caches it and only regenerates when this member changes.
/// <see cref="IsPartialProperty"/> selects the emit style (field-backed property vs. <c>field</c>-keyword partial
/// property). <see cref="Validates"/> emits a <c>ValidateProperty</c> call; <see cref="ValidationAttributes"/> are
/// reconstructed DataAnnotations attributes to re-emit on the property (field path only — a partial property already
/// carries them). A non-null <see cref="Warning"/> is reported alongside the emit.</summary>
internal sealed record BindableMemberInfo(
    string Namespace,
    string TypeKeyword,
    string TypeName,
    string FieldName,
    string PropertyName,
    string PropertyType,
    bool HasInpcBase,
    bool IsPartialProperty,
    bool Validates,
    EquatableArray<string> AffectsProperties,
    EquatableArray<string> AffectsCommands,
    EquatableArray<string> ValidationAttributes,
    string HintName,
    DiagnosticInfo Warning = null);
