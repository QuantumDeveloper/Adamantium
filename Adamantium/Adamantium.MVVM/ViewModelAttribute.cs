using System;

namespace Adamantium.MVVM;

/// <summary>
/// Put on a <c>partial class</c> that can't derive from <see cref="AdamantiumViewModel"/> (it already has a base):
/// the generator injects the <see cref="System.ComponentModel.INotifyPropertyChanged"/> implementation (+ a
/// protected <c>SetProperty</c>/<c>RaisePropertyChanged</c>) so its <c>[Bindable]</c> fields work. If the class is
/// free to derive, derive from <see cref="AdamantiumViewModel"/> instead and skip this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ViewModelAttribute : Attribute
{
}
