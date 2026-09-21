using System;

namespace Adamantium.MVVM;

/// <summary>
/// Put alongside <see cref="BindableAttribute"/> on a field to also raise change notification for other members
/// when this property changes — e.g. a computed property that depends on it:
/// <code>[Bindable, Affects(nameof(FullName))] private string _firstName;</code>
/// Name one or more members. Naming a generated COMMAND re-raises its <c>CanExecuteChanged</c> instead — which is how a
/// button or a menu row follows a command whose availability depends on this property.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class AffectsAttribute : Attribute
{
    public AffectsAttribute(params string[] memberNames) => MemberNames = memberNames;

    public string[] MemberNames { get; }
}
