using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adamantium.UI.Designer.Host;

/// <summary>
/// Long-running designer host: reads line-delimited JSON requests from stdin and writes line-delimited JSON
/// responses to stdout, reusing a single warm <see cref="DesignerSession"/> across requests. This is what the
/// Rider preview plugin talks to for live, rebuild-free updates.
///
/// Protocol (one JSON object per line):
///   -&gt; {"op":"render","text":"&lt;auml&gt;","scale":1.0,"width":1280,"height":720,"uri":"&lt;optional&gt;","live":true}
///        (width/height are an optional design-size hint; scale (default 1) zooms the render. The window is laid out
///         at its design size and the render target is design × scale. live=true captures the whole animation.)
///   &lt;- {"frames":["&lt;path0&gt;",...],"frameMs":16.7,"looped":false,"width":1280,"height":720,"scale":1.0,"diagnostics":[...]}
///        or {"error":"&lt;message&gt;","diagnostics":[...]}. Frames are RAW B8G8R8A8 (width*height*4 bytes, no encode);
///        a settled render returns one, a live render the full animation sequence the client plays at frameMs.
///   -&gt; {"op":"hittest","x":..,"y":..}   &lt;- {"hit":{"line":..,"column":..,"x":..,"y":..,"width":..,"height":..}}
///        (maps a design-space point to the authored element's markup position + rect; for hover / go-to-source)
///   -&gt; {"op":"select","x":..,"y":..}   &lt;- {"frames":[..],"hit":{"line":..,"column":..},..}
///        (selects the authored element at the point - the FRAMEWORK draws the selection frame - and returns the
///         updated frame plus the element's markup line/col for editor caret sync; a miss clears the selection)
///   -&gt; {"op":"hover","x":..,"y":..}   &lt;- {"frames":[..],..}
///        (the transient hover frame, also drawn by the FRAMEWORK; returns the updated frame. Omit x/y to clear it)
///   -&gt; {"op":"shutdown"}   (exits the process)
/// </summary>
public static class DesignerHost
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string _logPath;

    // A render failure must tell the user WHAT broke, not a bare "render failed". Format the exception chain (type +
    // message for the exception and every InnerException), the first stack frames (where it threw), and the path to the
    // full per-PID host log - everything the IDE's error badge needs to be actionable.
    private static string Describe(Exception e)
    {
        var sb = new StringBuilder();
        for (var cur = e; cur != null; cur = cur.InnerException)
        {
            if (sb.Length > 0) sb.Append("\n caused by ");
            sb.Append(cur.GetType().Name).Append(": ").Append(cur.Message);
        }
        if (!string.IsNullOrEmpty(e.StackTrace))
        {
            var frames = e.StackTrace.Split('\n');
            sb.Append('\n');
            for (var i = 0; i < frames.Length && i < 8; i++)
                sb.Append('\n').Append(frames[i].TrimEnd());
        }
        if (_logPath != null)
            sb.Append("\n\nFull host log: ").Append(_logPath);
        return sb.ToString();
    }

    public static int Run()
    {
        // Read/write the protocol as UTF-8 without a BOM so it round-trips cleanly with the plugin.
        TrySetUtf8Console();

        // Keep stdout strictly for the JSON protocol; route any stray engine chatter to stderr so it can't
        // corrupt the stream the plugin parses.
        var protocol = Console.Out;
        Console.SetOut(Console.Error);

        // Also mirror all engine chatter + any crash/exception output to a log file, so a designer failure is
        // diagnosable even when the IDE swallows the host's stderr. The protocol stays on the real stdout (saved above).
        try
        {
            // Per-PID file so a crash + respawn doesn't clobber the failing session's log.
            var logPath = Path.Combine(Path.GetTempPath(), $"adamantium-designer-host-{Environment.ProcessId}.log");
            _logPath = logPath;
            var fileLog = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            var sink = TextWriter.Synchronized(fileLog);
            Console.SetError(sink);
            Console.SetOut(sink);
        }
        catch { /* logging is best-effort; never block the host on it */ }

        DesignerSession session;
        try
        {
            session = new DesignerSession();
        }
        catch (Exception e)
        {
            WriteResponse(protocol, new Response { Error = $"init failed: {Describe(e)}" });
            return 1;
        }

        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "adamantium-designer")).FullName;
        long counter = 0;
        var previousFrames = new List<string>();

        string line;
        while ((line = Console.In.ReadLine()) != null)
        {
            line = line.TrimStart((char)0xFEFF).Trim(); // tolerate a leading BOM / surrounding whitespace
            if (line.Length == 0) continue;

            Request request;
            try { request = JsonSerializer.Deserialize<Request>(line, JsonOptions); }
            catch (Exception e) { WriteResponse(protocol, new Response { Error = $"bad request: {e.Message}" }); continue; }

            // Log everything except the high-frequency streaming "frame" op (would flood the log 60x/s).
            if (request?.Op != "frame")
                Console.Error.WriteLine($"[req] op={request?.Op} scale={request?.Scale} x={request?.X} y={request?.Y}");

            switch (request?.Op)
            {
                case "shutdown":
                    session.Dispose();
                    return 0;

                case "render":
                    var outPath = Path.Combine(tempDir, $"preview-{counter++}.bgra");
                    WriteResponse(protocol, RenderOne(session, request, outPath, previousFrames));
                    break;

                case "frame":
                    // Advance the live preview one frame (real-time animation streaming). The client calls this in a
                    // loop while the previous response said Animating; each frame is a fresh file (old ones cleaned up).
                    var framePath = Path.Combine(tempDir, $"preview-{counter++}.bgra");
                    WriteResponse(protocol, FrameOne(session, framePath, previousFrames));
                    break;

                case "hittest":
                    WriteResponse(protocol, HitTestOne(session, request));
                    break;

                case "select":
                    var selPath = Path.Combine(tempDir, $"preview-{counter++}.bgra");
                    WriteResponse(protocol, SelectOne(session, request, selPath, previousFrames));
                    break;

                case "hover":
                    var hovPath = Path.Combine(tempDir, $"preview-{counter++}.bgra");
                    WriteResponse(protocol, HoverOne(session, request, hovPath, previousFrames));
                    break;

                default:
                    WriteResponse(protocol, new Response { Error = $"unknown op '{request?.Op}'" });
                    break;
            }
        }

        session.Dispose();
        return 0;
    }

    private static void TrySetUtf8Console()
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        try { Console.InputEncoding = utf8NoBom; } catch { /* stdin may be redirected */ }
        try { Console.OutputEncoding = utf8NoBom; } catch { /* stdout may be redirected */ }
    }

    private static Response RenderOne(DesignerSession session, Request request, string outPath, List<string> previousFrames)
    {
        try
        {
            var result = session.Render(request.Text ?? string.Empty, request.Width, request.Height, request.Scale ?? 1.0, outPath, request.Uri, request.Live ?? false);
            if (!result.Success)
                return new Response { Error = result.Error, Diagnostics = NullIfEmpty(result.Diagnostics) };

            // The plugin loads each frame fully on receipt, so the previous render's frames are safe to delete now.
            foreach (var f in previousFrames)
                if (File.Exists(f)) { try { File.Delete(f); } catch { /* best effort */ } }
            previousFrames.Clear();
            previousFrames.AddRange(result.Frames);

            return new Response
            {
                Frames = result.Frames.ToArray(),
                Animating = result.Animating,
                Width = result.Width,
                Height = result.Height,
                DesignWidth = result.DesignWidth,
                DesignHeight = result.DesignHeight,
                Scale = result.Scale,
                Diagnostics = NullIfEmpty(result.Diagnostics)
            };
        }
        catch (Exception e)
        {
            return new Response { Error = Describe(e) };
        }
    }

    // Advances the live preview one frame for the animation stream (op "frame"). Deletes the previous frame file (the
    // client has already loaded it) and returns the new one plus whether anything is still animating.
    private static Response FrameOne(DesignerSession session, string outPath, List<string> previousFrames)
    {
        try
        {
            var result = session.RenderNextFrame(outPath);
            if (!result.Success)
                return new Response { Error = result.Error };

            foreach (var f in previousFrames)
                if (File.Exists(f)) { try { File.Delete(f); } catch { /* best effort */ } }
            previousFrames.Clear();
            previousFrames.AddRange(result.Frames);

            return new Response
            {
                Frames = result.Frames.ToArray(),
                Animating = result.Animating,
                Width = result.Width,
                Height = result.Height,
                DesignWidth = result.DesignWidth,
                DesignHeight = result.DesignHeight,
                Scale = result.Scale
            };
        }
        catch (Exception e)
        {
            return new Response { Error = Describe(e) };
        }
    }

    // Maps a point (in the last render's design space) to the authored element under it: its markup position
    // (line/column) and rect, for the designer's go-to-source (click) and hover frame. 'hit' is omitted when
    // nothing authored sits there.
    private static Response HitTestOne(DesignerSession session, Request request)
    {
        try
        {
            var hit = session.HitTest(request.X ?? 0, request.Y ?? 0);
            return hit is null
                ? new Response()
                : new Response
                {
                    Hit = new HitInfo
                    {
                        Line = hit.Line,
                        Column = hit.Position,
                        X = hit.X,
                        Y = hit.Y,
                        Width = hit.Width,
                        Height = hit.Height
                    }
                };
        }
        catch (Exception e)
        {
            return new Response { Error = Describe(e) };
        }
    }

    // Selects the authored element at a design-space point: the FRAMEWORK draws the selection frame (the window's
    // AdornerLayer), so the response carries the updated frame plus the element's markup line/col for editor caret
    // sync. A miss clears the selection (frame returned without a Hit).
    private static Response SelectOne(DesignerSession session, Request request, string outPath, List<string> previousFrames)
    {
        try
        {
            var result = session.SelectAt(request.X ?? 0, request.Y ?? 0, outPath);
            if (!result.Success)
                return new Response { Error = result.Error };

            foreach (var f in previousFrames)
                if (File.Exists(f)) { try { File.Delete(f); } catch { /* best effort */ } }
            previousFrames.Clear();
            previousFrames.AddRange(result.Frames);

            return new Response
            {
                Frames = result.Frames.ToArray(),
                Animating = result.Animating,
                Width = result.Width,
                Height = result.Height,
                DesignWidth = result.DesignWidth,
                DesignHeight = result.DesignHeight,
                Scale = result.Scale,
                Hit = result.Hit is { } h
                    ? new HitInfo { Line = h.Line, Column = h.Position, X = h.X, Y = h.Y, Width = h.Width, Height = h.Height }
                    : null
            };
        }
        catch (Exception e)
        {
            return new Response { Error = Describe(e) };
        }
    }

    // Hovers the authored element at a design-space point: the FRAMEWORK draws the (stroke-aware) hover frame, so the
    // response carries the updated frame. An absent point (no x/y), a miss, or an already-selected element clears it.
    private static Response HoverOne(DesignerSession session, Request request, string outPath, List<string> previousFrames)
    {
        try
        {
            var result = session.Hover(request.X, request.Y, outPath);
            if (!result.Success)
                return new Response { Error = result.Error };

            foreach (var f in previousFrames)
                if (File.Exists(f)) { try { File.Delete(f); } catch { /* best effort */ } }
            previousFrames.Clear();
            previousFrames.AddRange(result.Frames);

            return new Response
            {
                Frames = result.Frames.ToArray(),
                Animating = result.Animating,
                Width = result.Width,
                Height = result.Height,
                DesignWidth = result.DesignWidth,
                DesignHeight = result.DesignHeight,
                Scale = result.Scale
            };
        }
        catch (Exception e)
        {
            return new Response { Error = Describe(e) };
        }
    }

    private static List<string> NullIfEmpty(List<string> diagnostics) =>
        diagnostics is { Count: > 0 } ? diagnostics : null;

    private static void WriteResponse(TextWriter protocol, Response response)
    {
        if (response.Error != null) Console.Error.WriteLine($"[resp] ERROR: {response.Error}");
        protocol.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
        protocol.Flush();
    }

    private sealed class Request
    {
        public string Op { get; set; }
        public string Text { get; set; }
        public uint? Width { get; set; }
        public uint? Height { get; set; }
        public double? Scale { get; set; }
        public string Uri { get; set; }
        // render: capture design-time animations (live preview) instead of the settled state.
        public bool? Live { get; set; }
        // hittest: a point in the last render's design space.
        public double? X { get; set; }
        public double? Y { get; set; }
    }

    private sealed class Response
    {
        public string[] Frames { get; set; }
        public bool? Animating { get; set; }
        public uint? Width { get; set; }
        public uint? Height { get; set; }
        public uint? DesignWidth { get; set; }
        public uint? DesignHeight { get; set; }
        public double? Scale { get; set; }
        public string Error { get; set; }
        public List<string> Diagnostics { get; set; }
        public HitInfo Hit { get; set; }
    }

    private sealed class HitInfo
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
