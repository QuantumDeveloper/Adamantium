package com.adamantium.auml

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.ide.plugins.PluginManagerCore
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.extensions.PluginId
import com.intellij.openapi.project.Project
import com.redhat.devtools.lsp4ij.LanguageServerFactory
import com.redhat.devtools.lsp4ij.server.OSProcessStreamConnectionProvider
import com.redhat.devtools.lsp4ij.server.StreamConnectionProvider
import java.nio.file.Files
import java.nio.file.Path
import java.security.MessageDigest
import java.util.zip.ZipInputStream
import kotlin.io.path.exists

/**
 * Wires the Adamantium AUML language server into LSP4IJ. LSP4IJ calls this to obtain a connection
 * provider, which launches the server executable and talks LSP over stdio.
 */
class AumlLanguageServerFactory : LanguageServerFactory {
    override fun createConnectionProvider(project: Project): StreamConnectionProvider =
        AumlLanguageServer()
}

private const val SERVER_EXE = "Adamantium.UI.LanguageServer.exe"
private const val PLUGIN_ID = "com.adamantium.auml"

// Human-readable label for the cache dir. Real freshness comes from the bundled server's content
// hash (see extractBundledServer) — a per-version key went stale across same-version dev rebuilds:
// the first extraction was reused and every newly-bundled server was silently ignored.
private val BUNDLE_VERSION: String =
    PluginManagerCore.getPlugin(PluginId.getId(PLUGIN_ID))?.version ?: "dev"

private class AumlLanguageServer : OSProcessStreamConnectionProvider() {
    init {
        super.setCommandLine(GeneralCommandLine(serverExecutable().toString()))
    }

    private fun serverExecutable(): Path {
        // Dev override: point straight at a local build.
        System.getenv("ADAMANTIUM_AUML_SERVER")?.let { return Path.of(it) }
        return extractBundledServer()
    }

    /**
     * Unpacks the server bundled in the plugin (/server/server.zip) into a cache dir keyed by the
     * zip's content hash, so a freshly built server is always re-extracted. (A per-version key went
     * stale across same-version dev rebuilds: the old extraction was reused and new fixes ignored.)
     */
    private fun extractBundledServer(): Path {
        val cacheDir = Path.of(PathManager.getSystemPath(), "adamantium-auml", "$BUNDLE_VERSION-${bundledServerHash()}")
        val exe = cacheDir.resolve(SERVER_EXE)
        if (exe.exists()) return exe

        Files.createDirectories(cacheDir)
        val bundled = javaClass.getResourceAsStream("/server/server.zip")
            ?: error("Bundled AUML server not found. Set ADAMANTIUM_AUML_SERVER to a local build, or rebuild the plugin.")

        bundled.use { stream ->
            ZipInputStream(stream).use { zip ->
                var entry = zip.nextEntry
                while (entry != null) {
                    val target = cacheDir.resolve(entry.name).normalize()
                    if (entry.isDirectory) {
                        Files.createDirectories(target)
                    } else {
                        Files.createDirectories(target.parent)
                        Files.newOutputStream(target).use { zip.copyTo(it) }
                    }
                    entry = zip.nextEntry
                }
            }
        }
        exe.toFile().setExecutable(true)
        return exe
    }

    /** Short content hash of the bundled server.zip — changes whenever the server is rebuilt. */
    private fun bundledServerHash(): String {
        val stream = javaClass.getResourceAsStream("/server/server.zip")
            ?: error("Bundled AUML server not found. Set ADAMANTIUM_AUML_SERVER to a local build, or rebuild the plugin.")
        val digest = MessageDigest.getInstance("SHA-256")
        stream.use { input ->
            val buffer = ByteArray(64 * 1024)
            while (true) {
                val read = input.read(buffer)
                if (read < 0) break
                digest.update(buffer, 0, read)
            }
        }
        return digest.digest().joinToString("") { "%02x".format(it) }.take(16)
    }
}
