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

    /// <summary>The window's design size (before scale) - what was authored. Reported so the editor can show the true
    /// size directly instead of reconstructing it as pixelSize / scale, which loses ±1px at fractional (auto-fit) scales.</summary>
    public uint DesignWidth { get; private init; }
    public uint DesignHeight { get; private init; }

    /// <summary>The scale the render actually used (may be below the requested scale when clamped to the size cap).</summary>
    public double Scale { get; private init; }

    public static RenderResult Ok(string pngPath, List<string> diagnostics, uint width, uint height, double scale,
        uint designWidth, uint designHeight) =>
        new() { Success = true, PngPath = pngPath, Diagnostics = diagnostics, Width = width, Height = height, Scale = scale,
            DesignWidth = designWidth, DesignHeight = designHeight };

    public static RenderResult Fail(string error, List<string> diagnostics) =>
        new() { Success = false, Error = error, Diagnostics = diagnostics };
}
