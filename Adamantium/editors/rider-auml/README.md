# Adamantium AUML — Rider / IntelliJ plugin

A packaged plugin so end users get everything with **zero settings**: install it and `.auml`
files are XML-highlighted and get completion + diagnostics from the Adamantium AUML language
server. Built on **LSP4IJ** (installed automatically as a dependency).

## Prerequisites

- **JDK 21** (already installed).
- **IntelliJ IDEA Community Edition** — recommended for building (it provides Gradle and the
  wrapper, and lets you run the `buildPlugin` task). You can also build from the CLI if you have
  Gradle.
- **LSP4IJ** plugin installed in Rider (already done) — needed at runtime; the build also
  references it.
- **.NET 10 SDK** on PATH — `buildPlugin` publishes the language server automatically
  (`dotnet publish`) and bundles it inside the plugin. End users need only the **.NET 10 runtime**.

## 1. Set versions (one-time)

Edit `gradle.properties`:
- `platformVersion` / `sinceBuild` — pick an IntelliJ version **≤ your Rider's** version
  (Rider 2025.2 → `2025.2` / `252`, etc.).
- `lsp4ijPlugin` — set to the LSP4IJ version you have installed
  (Rider → Settings → Plugins → LSP4IJ → version).

## 2. Build

**In IntelliJ IDEA Community (easiest):** open this folder (`editors/rider-auml`), let Gradle
sync, then run the Gradle task **`buildPlugin`** (Gradle tool window → Tasks → intellij platform).

**From the CLI** (needs the Gradle wrapper — IDEA creates it, or run `gradle wrapper` once):
```
./gradlew buildPlugin
```

Either way the result is: `build/distributions/adamantium-auml-0.1.0.zip`.

## 3. Install in Rider

Settings → Plugins → ⚙ → **Install Plugin from Disk…** → select the zip → restart.

Open any `.auml` file: it should be XML-highlighted, and completion (`Ctrl+Space`) + red-squiggle
diagnostics should work — **no manual file-type or server configuration**.

## Notes

- The **language server is bundled inside the plugin**: `buildPlugin` runs `dotnet publish`
  (framework-dependent) and packs the output as `/server/server.zip`; on first use the plugin
  unpacks it to a per-version cache dir and launches it. End users need only the **.NET 10 runtime**.
- For local development, set the `ADAMANTIUM_AUML_SERVER` environment variable to a built
  `Adamantium.UI.LanguageServer.exe` to skip the bundled copy.
- After changing the server, rebuild the plugin (the publish reruns) and bump `BUNDLE_VERSION` in
  `AumlLanguageServerFactory.kt` so the cached copy is refreshed.
