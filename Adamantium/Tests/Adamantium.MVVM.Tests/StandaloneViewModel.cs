using Adamantium.MVVM;

namespace Adamantium.MVVM.Tests;

/// <summary>Sample VM that can't derive from <see cref="AdamantiumViewModel"/> (imagine a different base): the
/// <c>[ViewModel]</c> attribute makes the generator inject the INPC implementation so its [Bindable] field works.</summary>
[ViewModel]
public partial class StandaloneViewModel
{
    [Bindable] private int _counter;
}
