import * as fs from 'fs';
import * as path from 'path';
import { workspace, window, ExtensionContext } from 'vscode';
import { LanguageClient, LanguageClientOptions, ServerOptions } from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

const SERVER_EXE = 'Adamantium.UI.LanguageServer.exe';

export function activate(context: ExtensionContext): void {
  // Default: the language server bundled inside the extension (resolved relative to the install dir,
  // so there are no machine-specific paths). Override with "auml.serverPath" to point at a local
  // build during development.
  const configured = workspace.getConfiguration('auml').get<string>('serverPath');
  const serverPath = configured && configured.length > 0
    ? configured
    : context.asAbsolutePath(path.join('server', SERVER_EXE));

  if (!fs.existsSync(serverPath)) {
    window.showErrorMessage(
      `Adamantium AUML: language server not found at "${serverPath}". ` +
      'Reinstall the extension, or set "auml.serverPath" to a local build.');
    return;
  }

  // The server talks LSP over stdio; spawning the executable is all that is needed.
  const serverOptions: ServerOptions = {
    run: { command: serverPath },
    debug: { command: serverPath },
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'auml' }],
  };

  client = new LanguageClient('auml', 'Adamantium AUML', serverOptions, clientOptions);
  client.start();
}

export function deactivate(): Thenable<void> | undefined {
  return client?.stop();
}
