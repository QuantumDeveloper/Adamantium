using Adamantium.UI.LanguageServer;

// Modes:
//   (no args)    run as an LSP server over stdio — this is how an editor launches us
//   --selftest   run the in-process LSP integration test and exit
//   --demo       run the console completion demo and exit
if (args.Contains("--demo"))
{
    var bin = FindDefaultBinDir();
    if (bin is null)
    {
        Console.Error.WriteLine("[auml] playground bin not found; build Adamantium.UI.Sandbox first");
        return 1;
    }
    RunDemo(new CompletionEngine(AumlWorkspace.BuildFromBin(bin)));
    return 0;
}
if (args.Contains("--selftest"))
{
    return LspSelfTest.Run();
}

// Project-aware: the workspace resolves a type model per project from the opened file's build output.
var server = new LspServer(new AumlWorkspace(), Console.OpenStandardInput(), Console.OpenStandardOutput());
server.Run();
return 0;

static void RunDemo(CompletionEngine engine)
{
    foreach (var marked in new[]
    {
        "<Bor|",
        "<Border |",
        "<Border Back|",
        "<Border HorizontalAlignment=\"|\"",
        "<Border ClipToBounds=\"|\"",
        "<Border Background=\"|\"",
        "<RenderTargetPanel>\n    <Bor|",
    })
    {
        int caret = marked.IndexOf('|');
        string text = marked.Remove(caret, 1);
        var ctx = AumlCaretContext.Detect(text, caret);
        var items = engine.Complete(text, caret);
        Console.WriteLine($"\n>>> \"{marked.Replace("\n", "\\n")}\"  [{ctx.Kind}" +
                          $"{(ctx.ElementName is { Length: > 0 } e ? $" {e}" : "")}" +
                          $"{(ctx.AttributeName is { Length: > 0 } a ? $".{a}" : "")} '{ctx.Prefix}']");
        var shown = items.Take(12).Select(i => i.Detail is null ? i.Label : $"{i.Label}:{i.Detail}");
        Console.WriteLine($"    {items.Count}: {string.Join(", ", shown)}{(items.Count > 12 ? ", ..." : "")}");
    }
}

static string? FindDefaultBinDir()
{
    var root = @"c:\AdamantiumEngine\Adamantium\output\Adamantium.UI.Sandbox\bin";
    if (!Directory.Exists(root)) return null;
    return Directory.EnumerateDirectories(root, "net*", SearchOption.AllDirectories)
        .Where(d => File.Exists(Path.Combine(d, "Adamantium.UI.dll")))
        .OrderByDescending(Directory.GetLastWriteTimeUtc)
        .FirstOrDefault();
}
