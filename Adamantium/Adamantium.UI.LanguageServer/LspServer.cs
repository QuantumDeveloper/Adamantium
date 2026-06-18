using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Minimal Language Server Protocol server over a byte stream (stdio): lifecycle, full-text
/// document sync, completion, and parse diagnostics. The JSON-RPC framing is hand-rolled so
/// the only dependency is System.Text.Json and the wire format stays easy to follow and test.
/// </summary>
public sealed class LspServer
{
    private readonly AumlWorkspace _workspace;
    private readonly Stream _input;
    private readonly Stream _output;
    // Concurrent: the message loop mutates it while the workspace's bin-watcher thread reads it during auto-revalidation.
    private readonly ConcurrentDictionary<string, string> _documents = new();
    private readonly object _writeLock = new();

    public LspServer(AumlWorkspace workspace, Stream input, Stream output)
    {
        _workspace = workspace;
        _input = input;
        _output = output;
        // A rebuild refreshes the type model -> re-validate open docs so stale "unknown property/type" squiggles
        // clear automatically, without the user having to touch the file or restart the server.
        _workspace.ModelsChanged += RevalidateOpenDocuments;
    }

    private void RevalidateOpenDocuments()
    {
        foreach (var uri in _documents.Keys)
            PublishDiagnostics(uri);
    }

    public void Run()
    {
        while (true)
        {
            JsonNode? message;
            try { message = ReadMessageFrom(_input); }
            catch { break; }
            if (message is null) break;                 // stream closed

            try { if (Dispatch(message)) break; }
            catch (Exception ex) { Console.Error.WriteLine($"[auml] dispatch error: {ex.Message}"); }
        }
    }

    /// <summary>Handles one message; returns true when the server should exit.</summary>
    private bool Dispatch(JsonNode msg)
    {
        var method = msg["method"]?.GetValue<string>();
        var id = msg["id"];

        switch (method)
        {
            case "initialize":
                Reply(id, InitializeResult());
                break;

            case "textDocument/didOpen":
            {
                var td = msg["params"]!["textDocument"]!;
                var uri = td["uri"]!.GetValue<string>();
                _documents[uri] = td["text"]!.GetValue<string>();
                PublishDiagnostics(uri);
                break;
            }

            case "textDocument/didChange":
            {
                var p = msg["params"]!;
                var uri = p["textDocument"]!["uri"]!.GetValue<string>();
                var changes = p["contentChanges"]!.AsArray();
                if (changes.Count > 0)                  // full sync: the last change carries the whole text
                    _documents[uri] = changes[^1]!["text"]!.GetValue<string>();
                PublishDiagnostics(uri);
                break;
            }

            case "textDocument/didClose":
                _documents.TryRemove(msg["params"]!["textDocument"]!["uri"]!.GetValue<string>(), out _);
                break;

            case "textDocument/completion":
                Reply(id, CompletionResult(msg["params"]!));
                break;

            case "textDocument/hover":
                Reply(id, HoverResult(msg["params"]!));
                break;

            case "textDocument/documentSymbol":
                Reply(id, DocumentSymbolResult(msg["params"]!));
                break;

            case "textDocument/codeAction":
                Reply(id, CodeActionResult(msg["params"]!));
                break;

            case "textDocument/semanticTokens/full":
                Reply(id, SemanticTokensResult(msg["params"]!));
                break;

            case "shutdown":
                Reply(id, null);
                break;

            case "exit":
                return true;

            default:
                if (id is not null) Reply(id, null);   // answer unknown requests so the client doesn't wait
                break;
        }

        return false;
    }

    private static JsonNode InitializeResult() => new JsonObject
    {
        ["capabilities"] = new JsonObject
        {
            ["textDocumentSync"] = 1,                    // 1 = full document sync
            ["completionProvider"] = new JsonObject
            {
                ["triggerCharacters"] = new JsonArray { "<", " ", "\"", "=", ".", ":" }
            },
            ["hoverProvider"] = true,
            ["documentSymbolProvider"] = true,
            ["codeActionProvider"] = true,
            ["semanticTokensProvider"] = new JsonObject
            {
                ["legend"] = new JsonObject
                {
                    ["tokenTypes"] = new JsonArray { "namespace", "type", "property", "macro", "unknown" },
                    ["tokenModifiers"] = new JsonArray()
                },
                ["full"] = true
            }
        },
        ["serverInfo"] = new JsonObject { ["name"] = "Adamantium AUML Language Server", ["version"] = "0.1.0" }
    };

    private JsonNode CompletionResult(JsonNode @params)
    {
        var uri = @params["textDocument"]!["uri"]!.GetValue<string>();
        var pos = @params["position"]!;
        if (!_documents.TryGetValue(uri, out var text))
            return new JsonArray();

        var model = ResolveModel(uri);
        if (model is null) return new JsonArray();

        int offset = OffsetAt(text, pos["line"]!.GetValue<int>(), pos["character"]!.GetValue<int>());
        var result = new JsonArray();
        foreach (var item in new CompletionEngine(model).Complete(text, offset))
        {
            var node = new JsonObject { ["label"] = item.Label, ["kind"] = LspKind(item.Kind) };
            if (item.Detail is not null) node["detail"] = item.Detail;
            if (item.InsertText is not null)
            {
                node["insertText"] = item.InsertText;
                node["insertTextFormat"] = 2;   // Snippet — the $0 places the caret between the inserted quotes
            }
            result.Add(node);
        }
        return result;
    }

    private AumlTypeModel? ResolveModel(string uri)
    {
        try { return _workspace.GetModelForFile(UriToLocalPath(uri)); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[auml] cannot resolve project for {uri}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts an LSP document URI to a local filesystem path. VS Code percent-encodes the drive
    /// colon (file:///c%3A/…); .NET's <c>Uri.LocalPath</c> then yields "/c:/…" with a leading slash,
    /// which later turns into "c:\c:\…" via Path.GetFullPath. Strip that leading slash so both
    /// VS Code (c%3A) and Rider (C:) URIs resolve to the same path.
    /// </summary>
    private static string UriToLocalPath(string uri)
    {
        var path = new Uri(uri).LocalPath;
        if (path.Length >= 3 && path[0] is '/' or '\\' && char.IsLetter(path[1]) && path[2] == ':')
            path = path[1..];
        return path;
    }

    private JsonNode? HoverResult(JsonNode @params)
    {
        var uri = @params["textDocument"]!["uri"]!.GetValue<string>();
        var pos = @params["position"]!;
        if (!_documents.TryGetValue(uri, out var text)) return null;

        var model = ResolveModel(uri);
        if (model is null) return null;

        int offset = OffsetAt(text, pos["line"]!.GetValue<int>(), pos["character"]!.GetValue<int>());
        var hover = new HoverEngine(model).Hover(text, offset);
        if (hover is null) return null;

        return new JsonObject
        {
            ["contents"] = new JsonObject { ["kind"] = "markdown", ["value"] = hover }
        };
    }

    private JsonNode DocumentSymbolResult(JsonNode @params)
    {
        var uri = @params["textDocument"]!["uri"]!.GetValue<string>();
        var result = new JsonArray();
        if (_documents.TryGetValue(uri, out var text))
            foreach (var symbol in DocumentSymbolEngine.Symbols(text))
                result.Add(ToDocumentSymbol(symbol));
        return result;
    }

    private JsonNode SemanticTokensResult(JsonNode @params)
    {
        var uri = @params["textDocument"]!["uri"]!.GetValue<string>();
        var data = new JsonArray();
        if (_documents.TryGetValue(uri, out var text))
        {
            var model = ResolveModel(uri);

            // Delta-encode (LSP spec): each token is 5 ints relative to the previous one. Tokens come
            // out in document order, so a single forward walk converts offsets to line/character.
            int prevLine = 0, prevChar = 0;
            int offset = 0, line = 0, character = 0;
            foreach (var token in SemanticTokensEngine.Tokenize(text, model))
            {
                while (offset < token.Start)
                {
                    if (text[offset] == '\n') { line++; character = 0; } else character++;
                    offset++;
                }
                int deltaLine = line - prevLine;
                data.Add(deltaLine);
                data.Add(deltaLine == 0 ? character - prevChar : character);
                data.Add(token.Length);
                data.Add(token.TokenType);
                data.Add(0);
                prevLine = line;
                prevChar = character;
            }
        }
        return new JsonObject { ["data"] = data };
    }

    private JsonNode CodeActionResult(JsonNode @params)
    {
        var uri = @params["textDocument"]!["uri"]!.GetValue<string>();
        var result = new JsonArray();
        if (!_documents.TryGetValue(uri, out var text)) return result;

        var model = ResolveModel(uri);
        if (model is null) return result;

        var start = @params["range"]!["start"]!;
        int offset = OffsetAt(text, start["line"]!.GetValue<int>(), start["character"]!.GetValue<int>());

        // Echoing the triggering diagnostics back ties the fix to the red squiggle (lightbulb on the error).
        var triggers = @params["context"]?["diagnostics"] as JsonArray;

        foreach (var action in CodeActionEngine.CodeActions(text, model, offset))
        {
            var edits = new JsonArray();
            foreach (var edit in action.Edits)
                edits.Add(new JsonObject
                {
                    ["range"] = new JsonObject
                    {
                        ["start"] = new JsonObject { ["line"] = edit.StartLine, ["character"] = edit.StartCharacter },
                        ["end"] = new JsonObject { ["line"] = edit.EndLine, ["character"] = edit.EndCharacter }
                    },
                    ["newText"] = edit.NewText
                });

            var node = new JsonObject
            {
                ["title"] = action.Title,
                ["kind"] = "quickfix",
                ["edit"] = new JsonObject { ["changes"] = new JsonObject { [uri] = edits } }
            };
            if (triggers is { Count: > 0 }) node["diagnostics"] = triggers.DeepClone();
            result.Add(node);
        }
        return result;
    }

    private static JsonObject ToDocumentSymbol(AumlSymbol symbol)
    {
        var range = new JsonObject
        {
            ["start"] = new JsonObject { ["line"] = symbol.Line, ["character"] = symbol.Character },
            ["end"] = new JsonObject { ["line"] = symbol.Line, ["character"] = symbol.Character + symbol.Length }
        };
        var children = new JsonArray();
        foreach (var child in symbol.Children)
            children.Add(ToDocumentSymbol(child));

        var node = new JsonObject
        {
            ["name"] = symbol.Name,
            ["kind"] = 5,                 // LSP SymbolKind.Class
            ["range"] = range,
            ["selectionRange"] = range.DeepClone(),
            ["children"] = children
        };
        if (symbol.Detail.Length > 0) node["detail"] = symbol.Detail;
        return node;
    }

    // LSP CompletionItemKind: Class=7, Property=10, EnumMember=20.
    private static int LspKind(AumlCompletionItemKind kind) => kind switch
    {
        AumlCompletionItemKind.Element => 7,     // Class
        AumlCompletionItemKind.Property => 10,   // Property
        AumlCompletionItemKind.Value => 20,      // EnumMember
        AumlCompletionItemKind.Directive => 14,  // Keyword
        _ => 1
    };

    private void PublishDiagnostics(string uri)
    {
        var diagnostics = new JsonArray();
        if (_documents.TryGetValue(uri, out var text) && ResolveModel(uri) is { } model)
        {
            foreach (var d in AumlValidator.Validate(text, model))
                diagnostics.Add(Diagnostic(d.Line, d.Character, d.Line, d.Character + d.Length, d.Message));
        }
        Notify("textDocument/publishDiagnostics", new JsonObject { ["uri"] = uri, ["diagnostics"] = diagnostics });
    }

    private static JsonObject Diagnostic(int startLine, int startChar, int endLine, int endChar, string message) => new()
    {
        ["range"] = new JsonObject
        {
            ["start"] = new JsonObject { ["line"] = startLine, ["character"] = startChar },
            ["end"] = new JsonObject { ["line"] = endLine, ["character"] = endChar }
        },
        ["severity"] = 1,                                // 1 = Error
        ["source"] = "auml",
        ["message"] = message
    };

    private static int OffsetAt(string text, int line, int character)
    {
        int offset = 0;
        for (int current = 0; current < line; current++)
        {
            int newline = text.IndexOf('\n', offset);
            if (newline < 0) return text.Length;
            offset = newline + 1;
        }
        return Math.Min(offset + character, text.Length);
    }

    // --- JSON-RPC plumbing -------------------------------------------------

    private void Reply(JsonNode? id, JsonNode? result) => Write(new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result
    });

    private void Notify(string method, JsonNode @params) => Write(new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["method"] = method,
        ["params"] = @params
    });

    private void Write(JsonNode message)
    {
        var body = Encoding.UTF8.GetBytes(message.ToJsonString());
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        lock (_writeLock)
        {
            _output.Write(header, 0, header.Length);
            _output.Write(body, 0, body.Length);
            _output.Flush();
        }
    }

    /// <summary>Frames a raw JSON string as an LSP message (header + body). Used by tests.</summary>
    public static byte[] Frame(string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    /// <summary>Reads all framed messages from a stream until it ends. Used by tests.</summary>
    public static IEnumerable<JsonNode> ReadFrames(Stream stream)
    {
        while (ReadMessageFrom(stream) is { } node)
            yield return node;
    }

    private static JsonNode? ReadMessageFrom(Stream stream)
    {
        int contentLength = -1;
        string? line;
        while (!string.IsNullOrEmpty(line = ReadHeaderLine(stream)))
        {
            int colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                contentLength = int.Parse(line[(colon + 1)..].Trim());
        }
        if (line is null || contentLength < 0) return null;   // stream closed or no body

        var buffer = new byte[contentLength];
        int read = 0;
        while (read < contentLength)
        {
            int n = stream.Read(buffer, read, contentLength - read);
            if (n <= 0) return null;
            read += n;
        }
        return JsonNode.Parse(Encoding.UTF8.GetString(buffer));
    }

    private static string? ReadHeaderLine(Stream stream)
    {
        var sb = new StringBuilder();
        int prev = -1, current;
        while ((current = stream.ReadByte()) != -1)
        {
            if (prev == '\r' && current == '\n')
                return sb.ToString(0, sb.Length - 1);          // drop the trailing '\r'
            sb.Append((char)current);
            prev = current;
        }
        return null;                                            // EOF
    }
}
