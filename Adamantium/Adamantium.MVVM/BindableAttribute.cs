using System;

namespace Adamantium.MVVM;

/// <summary>
/// Put on a field to have the MVVM source generator emit a public property around it with change notification
/// (the <c>PropertyChangedBase.SetProperty</c> pattern). Field <c>_title</c>/<c>m_title</c>/<c>title</c> becomes
/// property <c>Title</c>. The generator also emits partial hooks <c>OnTitleChanging(value)</c> /
/// <c>OnTitleChanged(value)</c> you can implement. The containing class must derive from
/// <see cref="AdamantiumViewModel"/> or be marked <c>[ViewModel]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class BindableAttribute : Attribute
{
}
