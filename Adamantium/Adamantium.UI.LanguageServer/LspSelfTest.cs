using System.Text.Json.Nodes;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// In-process integration test of <see cref="LspServer"/>: feeds a scripted LSP session through
/// real framed streams and prints the server's framed responses. Exercises framing, dispatch,
/// per-project resolution, completion (elements / properties / x: directives / brush values),
/// diagnostics (semantic + parse errors), hover, and document symbols.
/// </summary>
internal static class LspSelfTest
{
    public static int Run()
    {
        const string dir = "file:///c:/AdamantiumEngine/Adamantium/Adamantium/Adamantium.UI.Playground";
        // VS Code percent-encodes the drive colon (file:///c%3A/...) — UriToLocalPath must still resolve the project.
        const string dirEncoded = "file:///c%3A/AdamantiumEngine/Adamantium/Adamantium/Adamantium.UI.Playground";
        const string encodedUri = dirEncoded + "/Encoded.auml";
        const string completionUri = dir + "/Completion.auml";
        const string diagUri = dir + "/Diag.auml";
        const string directiveUri = dir + "/Directive.auml";
        const string hoverUri = dir + "/Hover.auml";
        const string brushUri = dir + "/Brush.auml";
        const string missingUri = dir + "/Missing.auml";
        const string qualifyUri = dir + "/Qualify.auml";
        const string importUri = dir + "/Import.auml";
        const string clrUri = dir + "/Clr.auml";
        const string clrGoodUri = dir + "/ClrGood.auml";
        const string clrBadUri = dir + "/ClrBad.auml";
        const string propElementUri = dir + "/PropElement.auml";
        const string themePropUri = dir + "/ThemeProp.auml";
        const string attachedUri = dir + "/Attached.auml";
        const string xPrefixUri = dir + "/XPrefix.auml";
        const string semanticUri = dir + "/Semantic.auml";

        const string resourcesRoot = """<StyleSet xmlns="http://adamantium/ui/resources" xmlns:x="http://adamantium/ui/xaml/extensions" xmlns:controls="http://adamantium/ui">""";

        var (completionText, cLine, cChar) = Caret(
            """<StyleSet xmlns="http://adamantium/ui/resources" xmlns:controls="http://adamantium/ui"><controls:Border |></controls:Border></StyleSet>""");

        const string diagDoc = resourcesRoot +
            """<Style Selector="Button"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><controls:Border Backgroud="Blue"/></ControlTemplate></Setter.Value></Setter></Style></StyleSet>""";

        var (directiveText, dLine, dChar) = Caret(resourcesRoot + """<controls:Border x:|></controls:Border></StyleSet>""");

        const string hoverDoc = resourcesRoot + """<controls:Border x:Name="Inner" Background="Blue"/></StyleSet>""";
        var border = Pos(hoverDoc, "Border");
        var background = Pos(hoverDoc, "Background");

        var (brushText, brLine, brChar) = Caret(
            """<StyleSet xmlns="http://adamantium/ui/resources" xmlns:controls="http://adamantium/ui"><controls:Border Background="|"></controls:Border></StyleSet>""");

        const string missingDoc = "<Border></Border>";   // no xmlns -> "not in scope" + an import quick-fix
        var missingBorder = Pos(missingDoc, "Border");

        // <Border> uses the default 'resources' xmlns where it doesn't exist, but controls is already declared.
        const string qualifyDoc = """<StyleSet xmlns="http://adamantium/ui/resources" xmlns:controls="http://adamantium/ui"><Border/></StyleSet>""";
        var qualify = Pos(qualifyDoc, "Border");
        // Same, but controls is NOT declared -> the fix must add the xmlns to the root.
        const string importDoc = """<StyleSet xmlns="http://adamantium/ui/resources"><Border/></StyleSet>""";
        var import = Pos(importDoc, "Border");

        // clr-namespace: a prefix bound straight to a CLR namespace (XAML-style) -> complete its types.
        const string clrNs = "clr-namespace:Adamantium.UI.Controls.Decorators";
        var (clrText, clrLine, clrChar) = Caret(
            $"""<StyleSet xmlns="http://adamantium/ui/resources" xmlns:dec="{clrNs}"><dec:Bord|</StyleSet>""");
        // Existence validation: a real CLR namespace -> no diagnostic; a bogus one -> "not found".
        const string clrGoodDoc = $"""<StyleSet xmlns="http://adamantium/ui/resources" xmlns:dec="{clrNs}"><dec:Border/></StyleSet>""";
        const string clrBadDoc = """<StyleSet xmlns="http://adamantium/ui/resources" xmlns:bad="clr-namespace:No.Such.Namespace.Xyz"><bad:Thing/></StyleSet>""";

        // Property-element syntax <Owner.Property> -> complete the owner type's properties.
        var (propElementText, peLine, peChar) = Caret(
            """<StyleSet xmlns="http://adamantium/ui/resources" xmlns:controls="http://adamantium/ui"><controls:Border.Chi|</StyleSet>""");
        // Property element onto a get-only collection (Theme.StyleIncludes) — must still be offered.
        var (themePropText, tpLine, tpChar) = Caret(
            """<Theme xmlns="http://adamantium/ui/resources"><Theme.Sty|</Theme>""");
        // Semantic tokens: resolved element -> 'type', prefix -> 'namespace', attribute -> 'property', x: -> 'macro'.
        const string semanticDoc = """<StyleSet xmlns="http://adamantium/ui/resources" xmlns:controls="http://adamantium/ui" xmlns:x="http://adamantium/ui/xaml/extensions"><controls:Border Background="Red" x:Name="b"/><Bogus/></StyleSet>""";
        // Undeclared x: prefix (xmlns:x deleted) -> Alt+Enter on x:Name offers to declare the directives namespace.
        const string xPrefixDoc = """<Border xmlns="http://adamantium/ui" x:Name="root"></Border>""";
        var xName = Pos(xPrefixDoc, "x:Name");
        // Attached-property syntax Owner.Property="..." -> complete the owner type's attached properties.
        var (attachedText, atLine, atChar) = Caret(
            """<StyleSet xmlns="http://adamantium/ui/resources" xmlns:controls="http://adamantium/ui"><controls:Border ResourceContext.Sou|/></StyleSet>""");

        JsonNode[] session =
        {
            Request(1, "initialize"),
            Notification("initialized"),
            DidOpen(completionUri, completionText),
            Completion(2, completionUri, cLine, cChar),
            DidOpen(encodedUri, completionText),
            Completion(19, encodedUri, cLine, cChar),
            DidOpen(diagUri, diagDoc),
            DidOpen(directiveUri, directiveText),
            Completion(4, directiveUri, dLine, dChar),
            DidOpen(hoverUri, hoverDoc),
            Hover(5, hoverUri, border.Line, border.Ch),
            Hover(6, hoverUri, background.Line, background.Ch),
            DidOpen(brushUri, brushText),
            Completion(7, brushUri, brLine, brChar),
            DocumentSymbol(8, hoverUri),
            DidOpen(missingUri, missingDoc),
            CodeAction(16, missingUri, missingBorder.Line, missingBorder.Ch),
            DidOpen(qualifyUri, qualifyDoc),
            CodeAction(10, qualifyUri, qualify.Line, qualify.Ch),
            DidOpen(importUri, importDoc),
            CodeAction(11, importUri, import.Line, import.Ch),
            DidOpen(clrUri, clrText),
            Completion(12, clrUri, clrLine, clrChar),
            DidOpen(clrGoodUri, clrGoodDoc),
            DidOpen(clrBadUri, clrBadDoc),
            DidOpen(propElementUri, propElementText),
            Completion(13, propElementUri, peLine, peChar),
            DidOpen(themePropUri, themePropText),
            Completion(15, themePropUri, tpLine, tpChar),
            DidOpen(attachedUri, attachedText),
            Completion(14, attachedUri, atLine, atChar),
            DidOpen(xPrefixUri, xPrefixDoc),
            CodeAction(17, xPrefixUri, xName.Line, xName.Ch),
            DidOpen(semanticUri, semanticDoc),
            SemanticTokens(18, semanticUri),
            Request(9, "shutdown"),
            Notification("exit"),
        };

        var input = new MemoryStream();
        foreach (var message in session)
        {
            var framed = LspServer.Frame(message.ToJsonString());
            input.Write(framed, 0, framed.Length);
        }
        input.Position = 0;

        var output = new MemoryStream();
        new LspServer(new AumlWorkspace(), input, output).Run();

        output.Position = 0;
        foreach (var response in LspServer.ReadFrames(output))
        {
            var method = response["method"]?.GetValue<string>();
            var id = response["id"]?.ToJsonString();

            if (response["result"] is JsonArray array && id is "2" or "4" or "7" or "12" or "13" or "14" or "15" or "19")
            {
                var labels = array.Select(c => c!["label"]!.GetValue<string>()).ToList();
                var tag = id switch
                {
                    "4" => "x: directives",
                    "7" => "brush values",
                    "12" => "clr-namespace elements",
                    "13" => "property-element",
                    "14" => "attached property",
                    "15" => "property-element (collection)",
                    "19" => "encoded-URI (%3A) props",
                    _ => "controls:Border props"
                };
                Console.WriteLine($"<- completion ({tag}, id {id}): {labels.Count}: {string.Join(", ", labels.Take(8))}");
            }
            else if (id == "8" && response["result"] is JsonArray symbols)
            {
                var names = new List<string>();
                Flatten(symbols, names);
                Console.WriteLine($"<- documentSymbol (id 8): {string.Join(" > ", names)}");
            }
            else if (id is "10" or "11" or "16" or "17" && response["result"] is JsonArray actions)
            {
                Console.WriteLine($"<- codeAction (id {id}): {actions.Count} quick-fix(es)");
                foreach (var action in actions)
                {
                    Console.WriteLine($"     * {action!["title"]!.GetValue<string>()}");
                    foreach (var fileEdits in action["edit"]!["changes"]!.AsObject())
                        foreach (var edit in fileEdits.Value!.AsArray())
                        {
                            var s = edit!["range"]!["start"]!;
                            var newText = edit["newText"]!.GetValue<string>().Replace("\r", "\\r").Replace("\n", "\\n");
                            Console.WriteLine($"         L{s["line"]!.GetValue<int>()}:{s["character"]!.GetValue<int>()} -> \"{newText}\"");
                        }
                }
            }
            else if (id == "18" && response["result"]?["data"] is JsonArray tokenData)
            {
                var types = new[] { "namespace", "type", "property", "macro", "unknown" };
                var parts = new List<string>();
                int tokLine = 0, tokChar = 0;
                for (int k = 0; k + 4 < tokenData.Count; k += 5)
                {
                    int deltaLine = tokenData[k]!.GetValue<int>(), deltaChar = tokenData[k + 1]!.GetValue<int>();
                    int length = tokenData[k + 2]!.GetValue<int>(), tokenType = tokenData[k + 3]!.GetValue<int>();
                    if (deltaLine != 0) { tokLine += deltaLine; tokChar = deltaChar; } else tokChar += deltaChar;
                    parts.Add($"{types[tokenType]}@{tokLine}:{tokChar}+{length}");
                }
                Console.WriteLine($"<- semanticTokens (id 18): {tokenData.Count / 5} tokens: {string.Join(", ", parts)}");
            }
            else if (id is "5" or "6")
            {
                var value = response["result"]?["contents"]?["value"]?.GetValue<string>();
                Console.WriteLine($"<- hover (id {id}): {(value is null ? "(none)" : value.Replace("\n", " "))}");
            }
            else if (method == "textDocument/publishDiagnostics")
            {
                var @params = response["params"]!;
                var diags = @params["diagnostics"]!.AsArray();
                Console.WriteLine($"<- diagnostics [{Path.GetFileName(@params["uri"]!.GetValue<string>())}]: {diags.Count}");
                foreach (var d in diags)
                    Console.WriteLine($"     - {d!["message"]!.GetValue<string>()}");
            }
            else
            {
                Console.WriteLine($"<- {method ?? $"response id {id}"}");
            }
        }

        return 0;

        static void Flatten(JsonArray symbols, List<string> into)
        {
            foreach (var symbol in symbols)
            {
                var detail = symbol!["detail"]?.GetValue<string>();
                into.Add(symbol["name"]!.GetValue<string>() + (string.IsNullOrEmpty(detail) ? "" : $" {detail}"));
                if (symbol["children"] is JsonArray children) Flatten(children, into);
            }
        }

        static (string Text, int Line, int Character) Caret(string marked)
        {
            int index = marked.IndexOf('|');
            string text = marked.Remove(index, 1);
            var (line, character) = LineCol(text, index);
            return (text, line, character);
        }

        static (int Line, int Ch) Pos(string text, string needle)
        {
            var (line, character) = LineCol(text, text.IndexOf(needle, StringComparison.Ordinal));
            return (line, character);
        }

        static (int Line, int Character) LineCol(string text, int index)
        {
            int line = 0, character = 0;
            for (int i = 0; i < index; i++)
            {
                if (text[i] == '\n') { line++; character = 0; }
                else character++;
            }
            return (line, character);
        }

        static JsonObject Request(int id, string method) =>
            new() { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = new JsonObject() };

        static JsonObject Notification(string method) =>
            new() { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = new JsonObject() };

        static JsonObject DidOpen(string uri, string text) => new()
        {
            ["jsonrpc"] = "2.0", ["method"] = "textDocument/didOpen",
            ["params"] = new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri, ["text"] = text } }
        };

        static JsonObject Completion(int id, string uri, int line, int character) =>
            Positional(id, "textDocument/completion", uri, line, character);

        static JsonObject Hover(int id, string uri, int line, int character) =>
            Positional(id, "textDocument/hover", uri, line, character);

        static JsonObject DocumentSymbol(int id, string uri) => new()
        {
            ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = "textDocument/documentSymbol",
            ["params"] = new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri } }
        };

        static JsonObject SemanticTokens(int id, string uri) => new()
        {
            ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = "textDocument/semanticTokens/full",
            ["params"] = new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri } }
        };

        static JsonObject CodeAction(int id, string uri, int line, int character)
        {
            var position = new JsonObject { ["line"] = line, ["character"] = character };
            return new JsonObject
            {
                ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = "textDocument/codeAction",
                ["params"] = new JsonObject
                {
                    ["textDocument"] = new JsonObject { ["uri"] = uri },
                    ["range"] = new JsonObject { ["start"] = position, ["end"] = position.DeepClone() },
                    ["context"] = new JsonObject { ["diagnostics"] = new JsonArray() }
                }
            };
        }

        static JsonObject Positional(int id, string method, string uri, int line, int character) => new()
        {
            ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method,
            ["params"] = new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["position"] = new JsonObject { ["line"] = line, ["character"] = character }
            }
        };
    }
}
