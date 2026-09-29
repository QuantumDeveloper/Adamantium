namespace Adamantium.MVVM.Tests;

/// <summary>Overrides the hooks <see cref="OverridableHooksViewModel"/> was generated with.</summary>
public class DerivedHooksViewModel : OverridableHooksViewModel
{
    public string Changing;

    public string Changed;

    public int CountChanged;

    protected override void OnNameChanging(string value)
    {
        Changing = value;
    }

    protected override void OnNameChanged(string value)
    {
        Changed = value;
    }

    protected override void OnCountChanged(int value)
    {
        CountChanged = value;
    }
}
