namespace Adamantium.Navigation;

/// <summary>Optional on a content view model opened in its own window: lets it pick the window SHELL and set title/size
/// without touching any UI type. A <c>0</c> size or empty string means "use the shell's default".</summary>
public interface IWindowAware
{
    string WindowShellKey { get; }
    string Title { get; }
    double Width { get; }
    double Height { get; }
}
