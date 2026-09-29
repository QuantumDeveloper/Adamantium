using Adamantium.MVVM;

namespace Adamantium.MVVM.Tests;

/// <summary>[Bindable(Overridable = true)]: the hooks come out as empty protected virtual methods, so a derived class
/// can override them.</summary>
public partial class OverridableHooksViewModel : AdamantiumViewModel
{
    [Bindable(Overridable = true)] private string _name;

    [Bindable(Overridable = true)] public partial int Count { get; set; }
}
