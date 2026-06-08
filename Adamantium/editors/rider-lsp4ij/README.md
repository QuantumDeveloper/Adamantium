# Adamantium AUML in Rider via LSP4IJ

`Adamantium.UI.LanguageServer` speaks LSP over stdio, so Rider can use it through the
**LSP4IJ** plugin — no custom Rider plugin and no Node.js required.

## Steps

1. **Build the server:**

   ```
   dotnet build ..\..\Adamantium.UI.LanguageServer\Adamantium.UI.LanguageServer.csproj -c Debug -p:Platform=x64
   ```

   Produces:
   `Adamantium\output\Adamantium.UI.LanguageServer\bin\x64\Debug\net10.0\Adamantium.UI.LanguageServer.exe`

2. **Install LSP4IJ:** Rider → Settings → Plugins → Marketplace → search **"LSP4IJ"** (by Red Hat) → Install → restart.

3. **Register the server:** Settings → Languages & Frameworks → **Language Servers** → **+** (New Language Server):
   - **Name:** `Adamantium AUML`
   - **Command:** the full path to `Adamantium.UI.LanguageServer.exe` (run with **no arguments** — it defaults to LSP/stdio mode).
   - **Mappings** tab → **File name patterns** → add `*.auml`.

4. **Apply**, then open any `*.auml` file (e.g. `editors/vscode-auml/samples/Demo.auml`),
   put the caret inside a tag and press **Ctrl+Space** — you should see element / property / value completions.

## Notes

- Completion reflects the engine's real UI types, loaded from the built **Adamantium.UI.Playground** output.
- If nothing shows up, open LSP4IJ's **"LSP Consoles"** tool window and check the server's stderr —
  you should see `[auml] loading types from: ...`. Errors there explain a missing type model.
- This is the zero-build path for trying it out. A first-class Rider plugin (a thin Kotlin
  `LspServerSupportProvider` launcher) can replace LSP4IJ later for a packaged experience.
