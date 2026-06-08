# Adamantium AUML — VS Code extension

Language support for Adamantium UI markup (`.auml`): type-aware completion
(elements, settable properties, enum/boolean values) and basic diagnostics,
powered by `Adamantium.UI.LanguageServer` over LSP/stdio.

## Prerequisites

- **.NET 10 runtime** installed (the server is framework-dependent).
- **Node.js / npm** (to build the extension).
- The language server built:

  ```
  dotnet build ..\..\Adamantium.UI.LanguageServer\Adamantium.UI.LanguageServer.csproj -c Debug -p:Platform=x64
  ```

  This produces the exe the extension launches:
  `Adamantium\output\Adamantium.UI.LanguageServer\bin\x64\Debug\net10.0\Adamantium.UI.LanguageServer.exe`
  (override with the `auml.serverPath` setting if your path differs).

## Run it (Extension Development Host)

1. Open this folder (`editors/vscode-auml`) in VS Code.
2. `npm install`
3. Press **F5** ("Run AUML Extension"). A second VS Code window opens.
4. In that window, open `samples/Demo.auml`, put the caret inside the `<Border ...>`
   tag and type — you should see element / property / value completions.

## Notes

- The server currently loads the engine's UI types from the built **Adamantium.UI.Playground**
  output, so completion reflects the real control types regardless of which `.auml` file you open.
- Diagnostics presently flag malformed XML; richer AUML diagnostics are planned.
