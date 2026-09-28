using System;

namespace Adamantium.MVVM;

/// <summary>Emits change notification: a property for a field, or the body of a <c>partial</c> property, plus
/// <c>OnXChanging</c>/<c>OnXChanged</c> hooks. The class must be an <see cref="AdamantiumViewModel"/> or <c>[ViewModel]</c>.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class BindableAttribute : Attribute
{
}
