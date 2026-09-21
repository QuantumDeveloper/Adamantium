namespace Adamantium.UI.Designer.Host;

/// <summary>Outcome of a designer render: a single RAW B8G8R8A8 frame file (the current state of the live scene) or an
/// error, with loader diagnostics. Animations are played as a live stream - the client calls the "frame" op repeatedly
/// while <see cref="Animating"/> is true - rather than capturing a fixed sequence.</summary>
public sealed class RenderResult
{
    public bool Success { get; private init; }

    /// <summary>The rendered frame file, raw B8G8R8A8 (no encode). A single-element list (the current frame).</summary>
    public List<string> Frames { get; private init; }

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

    /// <summary>True while the live scene still has something to animate (a property animation/transition or an animated
    /// image). The client keeps requesting the next frame (op "frame") at ~60fps while this is true, then stops.</summary>
    public bool Animating { get; private init; }

    /// <summary>The element a selection (op "select") landed on - its markup line/column - so the editor can sync its
    /// caret. Null for ordinary renders and for a selection that hit nothing (the selection was cleared).</summary>
    public HitTestResult Hit { get; private init; }

    public static RenderResult Ok(List<string> frames, List<string> diagnostics, uint width, uint height, double scale,
        uint designWidth, uint designHeight, bool animating = false, HitTestResult hit = null) =>
        new() { Success = true, Frames = frames, Diagnostics = diagnostics, Width = width, Height = height, Scale = scale,
            DesignWidth = designWidth, DesignHeight = designHeight, Animating = animating, Hit = hit };

    public static RenderResult Fail(string error, List<string> diagnostics) =>
        new() { Success = false, Error = error, Diagnostics = diagnostics };
}
