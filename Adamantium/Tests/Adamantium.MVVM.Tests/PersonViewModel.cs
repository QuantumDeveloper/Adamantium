using Adamantium.MVVM;

namespace Adamantium.MVVM.Tests;

/// <summary>Sample VM exercising [Bindable], [Affects], [Command] and the partial change hook (the generator
/// fills in the FirstName/LastName properties, the SaveCommand, and calls OnFirstNameChanged).</summary>
public partial class PersonViewModel : AdamantiumViewModel
{
    [Bindable] private string _firstName = "";

    [Bindable, Affects(nameof(FullName))] private string _lastName = "";

    public string FullName => $"{FirstName} {LastName}".Trim();

    public int SaveCount;

    [Command(CanExecute = nameof(CanSave))]
    private void Save() => SaveCount++;

    private bool CanSave() => FirstName.Length > 0;

    public string LastChangedProperty;

    partial void OnFirstNameChanged(string value) => LastChangedProperty = nameof(FirstName);
}
