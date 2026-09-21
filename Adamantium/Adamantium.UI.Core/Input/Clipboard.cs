namespace Adamantium.UI.Core.Input;

/// <summary>
/// Text-clipboard facade. Defaults to an in-process clipboard (copy/paste works within the app with no platform code),
/// which a platform replaces with the real OS clipboard via <see cref="Current"/> at startup. Controls call
/// <see cref="GetText"/> / <see cref="SetText"/> and stay platform-agnostic.
/// </summary>
public static class Clipboard
{
    private sealed class InProcessClipboard : IClipboard
    {
        private string _text = string.Empty;
        public string GetText() => _text;
        public void SetText(string text) => _text = text ?? string.Empty;
    }

    /// <summary>The active provider. A platform sets this to the OS clipboard; until then an in-process store is used.</summary>
    public static IClipboard Current { get; set; } = new InProcessClipboard();

    public static string GetText() => Current?.GetText() ?? string.Empty;

    public static void SetText(string text) => Current?.SetText(text ?? string.Empty);
}
