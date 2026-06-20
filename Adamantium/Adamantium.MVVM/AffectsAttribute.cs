using System;

namespace Adamantium.MVVM;

/// <summary>
/// Put alongside <see cref="BindableAttribute"/> on a field to also raise change notification for other members
/// when this property changes — e.g. a computed property that depends on it:
/// <code>[Bindable, Affects(nameof(FullName))] private string _firstName;</code>
/// Name one or more members. (Affecting a generated command — to re-raise its CanExecuteChanged — lands once the
/// engine ICommand gains CanExecuteChanged; until then only property notifications are raised.)
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class AffectsAttribute : Attribute
{
    public AffectsAttribute(params string[] memberNames) => MemberNames = memberNames;

    public string[] MemberNames { get; }
}
