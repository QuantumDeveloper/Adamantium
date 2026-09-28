namespace Adamantium.MVVM.Generators;

/// <summary>What the generator needs to emit a <c>[Bindable]</c> member; value-equatable, so the member regenerates only when
/// it changes.</summary>
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
