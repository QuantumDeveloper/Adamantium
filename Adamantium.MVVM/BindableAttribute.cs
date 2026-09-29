using System;

namespace Adamantium.MVVM;

/// <summary>Emits change notification: a property for a field, or the body of a <c>partial</c> property, plus
/// <c>OnXChanging</c>/<c>OnXChanged</c> hooks. The class must be an <see cref="AdamantiumViewModel"/> or <c>[ViewModel]</c>.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class BindableAttribute : Attribute
{
    /// <summary>Emits the hooks as empty <c>protected virtual</c> methods instead of <c>partial</c> ones, so a derived
    /// class can override them.</summary>
    public bool Overridable { get; set; }
}
