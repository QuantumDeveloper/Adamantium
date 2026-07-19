namespace Adamantium.Navigation;

/// <summary>Maps a <see cref="DialogHostKind"/> to its <see cref="IDialogHost"/> (the dialog analogue of
/// RegionAdapterMappings). A new hosting medium = implement <see cref="IDialogHost"/> and <see cref="Register"/> it.</summary>
public interface IDialogHostRegistry
{
    void Register(DialogHostKind kind, IDialogHost host);

    IDialogHost Get(DialogHostKind kind);

    IDialogHost Default { get; }
}
