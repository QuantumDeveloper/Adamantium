namespace Adamantium.UI.Designer.Host;

/// <summary>Outcome of a designer render: one or more RAW B8G8R8A8 frame files (a single settled frame, or the whole
/// animation sequence for a live render) or an error, always with loader diagnostics.</summary>
public sealed class RenderResult
{
    public bool Success { get; private init; }

    /// <summary>The rendered frame file(s), raw B8G8R8A8 (no encode). One for a settled render; the full animation
    /// sequence (frame 0 = start) for a live render. The client plays them at <see cref="FrameMs"/>.</summary>
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

    /// <summary>Playback interval between sequence frames, in ms (0 for a single settled frame).</summary>
    public double FrameMs { get; private init; }

    /// <summary>True when the animation hit the frame cap without settling - the client loops the captured frames.</summary>
    public bool Looped { get; private init; }

    /// <summary>Frame index the client restarts the loop from (not 0): the point where one-shot window animations have
    /// settled, so they play once while looping media (animated images) keep cycling in the [LoopStart, end) tail.</summary>
    public int LoopStart { get; private init; }

    public static RenderResult Ok(List<string> frames, List<string> diagnostics, uint width, uint height, double scale,
        uint designWidth, uint designHeight, double frameMs = 0, bool looped = false, int loopStart = 0) =>
        new() { Success = true, Frames = frames, Diagnostics = diagnostics, Width = width, Height = height, Scale = scale,
            DesignWidth = designWidth, DesignHeight = designHeight, FrameMs = frameMs, Looped = looped, LoopStart = loopStart };

    public static RenderResult Fail(string error, List<string> diagnostics) =>
        new() { Success = false, Error = error, Diagnostics = diagnostics };
}
