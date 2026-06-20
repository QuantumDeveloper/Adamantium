using System.ComponentModel.DataAnnotations;
using Adamantium.MVVM;

namespace Adamantium.MVVM.Tests;

/// <summary>Validating VM: DataAnnotations attributes on the [Bindable] fields are forwarded by the generator onto
/// the generated properties, and the setters call ValidateProperty, surfacing errors via INotifyDataErrorInfo.</summary>
public partial class RegistrationViewModel : AdamantiumValidatingViewModel
{
    [Bindable, Required] private string _name;

    [Bindable, Range(0, 120)] private int _age;
}
