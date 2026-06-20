using System;

namespace Adamantium.MVVM;

/// <summary>
/// Put on a field <em>or</em> a <c>partial</c> property to have the MVVM source generator emit change-notification.
/// On a field (<c>_title</c>/<c>m_title</c>/<c>title</c>) it emits a public property <c>Title</c> using the
/// <c>PropertyChangedBase.SetProperty</c> pattern. On a <c>partial</c> property (<c>public partial string Title { get; set; }</c>)
/// it fills in the implementation (using the <c>field</c> keyword) — no dangling backing field. Either way it also
/// emits partial hooks <c>OnTitleChanging(value)</c> / <c>OnTitleChanged(value)</c> you can implement. The containing
/// class must derive from <see cref="AdamantiumViewModel"/> or be marked <c>[ViewModel]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class BindableAttribute : Attribute
{
}
