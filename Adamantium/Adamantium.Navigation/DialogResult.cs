namespace Adamantium.Navigation;

/// <summary>Default <see cref="IDialogResult"/>. A dialog view model raises <see cref="IDialogAware.RequestClose"/> with
/// one of these to close itself and return a value.</summary>
public sealed class DialogResult : IDialogResult
{
    public DialogResult(DialogButtonResult result, NavigationParameters parameters = null)
    {
        Result = result;
        Parameters = parameters ?? new NavigationParameters();
    }

    public DialogButtonResult Result { get; }
    public NavigationParameters Parameters { get; }

    public static DialogResult Ok(NavigationParameters parameters = null) => new(DialogButtonResult.Ok, parameters);
    public static DialogResult Cancel() => new(DialogButtonResult.Cancel);
    public static DialogResult Yes(NavigationParameters parameters = null) => new(DialogButtonResult.Yes, parameters);
    public static DialogResult No() => new(DialogButtonResult.No);
}
