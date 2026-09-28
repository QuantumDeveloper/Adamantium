using System.Threading;
using System.Threading.Tasks;
using Adamantium.MVVM;

namespace Adamantium.MVVM.Tests;

/// <summary>Sample VM exercising [Bindable], [Affects], sync + async [Command] and the partial change hook
/// (the generator fills in the FirstName/LastName properties, the sync SaveCommand, the async LoadCommand,
/// and calls OnFirstNameChanged).</summary>
public partial class PersonViewModel : AdamantiumViewModel
{
    [Bindable, Affects(nameof(SaveCommand))] private string _firstName = "";

    [Bindable, Affects(nameof(FullName))] private string _lastName = "";

    public string FullName => $"{FirstName} {LastName}".Trim();

    public int SaveCount;

    [Command(CanExecute = nameof(CanSave))]
    private void Save() => SaveCount++;

    private bool CanSave() => FirstName.Length > 0;

    public string LastChangedProperty;

    partial void OnFirstNameChanged(string value) => LastChangedProperty = nameof(FirstName);

    // Async command: a Task method with a CancellationToken → AdamantiumAsyncCommand. The gate lets a test
    // hold the command "running" to observe IsRunning / disable-while-running, then release or cancel it.
    public int LoadCount;
    public readonly TaskCompletionSource<bool> LoadGate = new();

    [Command]
    private async Task Load(CancellationToken token)
    {
        await LoadGate.Task.WaitAsync(token);
        LoadCount++;
    }

    // Typed sync command: a void method with one argument → AdamantiumCommand<string>.
    public string AppliedName;

    [Command]
    private void Apply(string name) => AppliedName = name;

    // Typed async command: a Task method with one argument + a token → AdamantiumAsyncCommand<string>.
    public string LoadedName;
    public readonly TaskCompletionSource<bool> ApplyGate = new();

    [Command]
    private async Task ApplyAsync(string name, CancellationToken token)
    {
        await ApplyGate.Task.WaitAsync(token);
        LoadedName = name;
    }
}
