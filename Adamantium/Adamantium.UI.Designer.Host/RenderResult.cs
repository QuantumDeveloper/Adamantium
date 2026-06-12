namespace Adamantium.UI.Designer.Host;

/// <summary>Outcome of a single designer render: either a PNG path (+ its pixel size) or an error, always with loader diagnostics.</summary>
public sealed class RenderResult
{
    public bool Success { get; private init; }
    public string PngPath { get; private init; }
    public string Error { get; private init; }
    public List<string> Diagnostics { get; private init; }
    public uint Width { get; private init; }
    public uint Height { get; private init; }

    public static RenderResult Ok(string pngPath, List<string> diagnostics, uint width, uint height) =>
        new() { Success = true, PngPath = pngPath, Diagnostics = diagnostics, Width = width, Height = height };

    public static RenderResult Fail(string error, List<string> diagnostics) =>
        new() { Success = false, Error = error, Diagnostics = diagnostics };
}
