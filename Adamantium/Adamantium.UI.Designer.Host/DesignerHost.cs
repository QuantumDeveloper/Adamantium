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
///   -&gt; {"op":"render","text":"&lt;auml&gt;","width":1280,"height":720,"uri":"&lt;optional&gt;"}
///   &lt;- {"png":"&lt;temp path&gt;","diagnostics":[...]}  or  {"error":"&lt;message&gt;","diagnostics":[...]}
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

    public static int Run()
    {
        // Read/write the protocol as UTF-8 without a BOM so it round-trips cleanly with the plugin.
        TrySetUtf8Console();

        // Keep stdout strictly for the JSON protocol; route any stray engine chatter to stderr so it can't
        // corrupt the stream the plugin parses.
        var protocol = Console.Out;
        Console.SetOut(Console.Error);

        DesignerSession session;
        try
        {
            session = new DesignerSession();
        }
        catch (Exception e)
        {
            WriteResponse(protocol, new Response { Error = $"init failed: {e.Message}" });
            return 1;
        }

        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "adamantium-designer")).FullName;
        long counter = 0;
        string previousPng = null;

        string line;
        while ((line = Console.In.ReadLine()) != null)
        {
            line = line.TrimStart((char)0xFEFF).Trim(); // tolerate a leading BOM / surrounding whitespace
            if (line.Length == 0) continue;

            Request request;
            try { request = JsonSerializer.Deserialize<Request>(line, JsonOptions); }
            catch (Exception e) { WriteResponse(protocol, new Response { Error = $"bad request: {e.Message}" }); continue; }

            switch (request?.Op)
            {
                case "shutdown":
                    session.Dispose();
                    return 0;

                case "render":
                    var outPath = Path.Combine(tempDir, $"preview-{counter++}.png");
                    WriteResponse(protocol, RenderOne(session, request, outPath, ref previousPng));
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

    private static Response RenderOne(DesignerSession session, Request request, string outPath, ref string previousPng)
    {
        try
        {
            var result = session.Render(request.Text ?? string.Empty, request.Width ?? 1280u, request.Height ?? 720u, outPath);
            if (!result.Success)
                return new Response { Error = result.Error, Diagnostics = NullIfEmpty(result.Diagnostics) };

            // The plugin loads the PNG fully on receipt, so the previous frame is safe to delete now.
            if (previousPng != null && File.Exists(previousPng))
            {
                try { File.Delete(previousPng); } catch { /* best effort */ }
            }
            previousPng = result.PngPath;

            return new Response { Png = result.PngPath, Diagnostics = NullIfEmpty(result.Diagnostics) };
        }
        catch (Exception e)
        {
            return new Response { Error = e.Message };
        }
    }

    private static List<string> NullIfEmpty(List<string> diagnostics) =>
        diagnostics is { Count: > 0 } ? diagnostics : null;

    private static void WriteResponse(TextWriter protocol, Response response)
    {
        protocol.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
        protocol.Flush();
    }

    private sealed class Request
    {
        public string Op { get; set; }
        public string Text { get; set; }
        public uint? Width { get; set; }
        public uint? Height { get; set; }
        public string Uri { get; set; }
    }

    private sealed class Response
    {
        public string Png { get; set; }
        public string Error { get; set; }
        public List<string> Diagnostics { get; set; }
    }
}
