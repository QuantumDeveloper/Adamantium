namespace Adamantium.UI.Core;

/// <summary>What a platform implements to answer <see cref="FileDialog"/>.</summary>
public interface IFileDialogPlatform
{
    /// <summary>Shows the platform's save dialog and returns the chosen path, or null when the user cancelled. Modal to
    /// the window the user pressed from, and called on the UI thread.</summary>
    string Save(SaveFileRequest request);
}
