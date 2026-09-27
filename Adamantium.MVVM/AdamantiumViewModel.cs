using Adamantium.Core;

namespace Adamantium.MVVM;

/// <summary>
/// Base class for view-models. Provides <see cref="System.ComponentModel.INotifyPropertyChanged"/> +
/// <c>SetProperty</c>/<c>RaisePropertyChanged</c> (via <see cref="PropertyChangedBase"/>), which the MVVM source
/// generator's <c>[Bindable]</c> properties build on. Derive from this; use <c>[ViewModel]</c> only when you must
/// keep a different base class.
/// </summary>
public abstract class AdamantiumViewModel : PropertyChangedBase
{
}
