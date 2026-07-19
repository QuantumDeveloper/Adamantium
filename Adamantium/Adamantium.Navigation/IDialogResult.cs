namespace Adamantium.Navigation;

/// <summary>The outcome of a dialog: which button closed it plus any values it hands back.</summary>
public interface IDialogResult
{
    DialogButtonResult Result { get; }
    NavigationParameters Parameters { get; }
}
