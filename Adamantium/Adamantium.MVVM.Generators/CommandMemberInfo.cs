namespace Adamantium.MVVM.Generators;

/// <summary>Value-equatable model for a <c>[Command]</c> method's generated <c>ICommand</c> property.
/// <see cref="CanExecuteExpression"/> is the prebuilt argument (e.g. <c>() => CanSave()</c>) or null.</summary>
internal sealed record CommandMemberInfo(
    string Namespace,
    string TypeKeyword,
    string TypeName,
    string MethodName,
    string CommandName,
    string FieldName,
    string CanExecuteExpression,
    string HintName);
