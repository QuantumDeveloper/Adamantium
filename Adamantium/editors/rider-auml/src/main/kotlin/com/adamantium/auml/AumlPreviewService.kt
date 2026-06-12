package com.adamantium.auml

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.openapi.Disposable
import com.intellij.openapi.components.Service
import com.intellij.openapi.diagnostic.logger
import java.io.BufferedReader
import java.io.BufferedWriter
import java.io.InputStreamReader
import java.io.OutputStreamWriter
import java.nio.charset.StandardCharsets
import java.nio.file.Path
import kotlin.io.path.exists

/**
 * Owns the long-running Adamantium designer host process and talks to it over its line-delimited JSON
 * protocol (see Adamantium.UI.Designer.Host). The host keeps the engine warm between renders, so a render
 * is just one request/response round-trip. One process is shared by every `.auml` editor in the project;
 * requests are serialised. The process is started lazily and restarted automatically if it dies.
 */
@Service(Service.Level.PROJECT)
class AumlPreviewService : Disposable {
    private val lock = Any()
    private var process: Process? = null
    private var writer: BufferedWriter? = null
    private var reader: BufferedReader? = null

    /** Result of one render: a PNG path on success, or an error; diagnostics are non-fatal notes either way. */
    data class RenderResult(val pngPath: String?, val error: String?, val diagnostics: List<String>)

    /**
     * Renders [text] at [width]x[height] and returns the produced PNG path (or an error). Blocking and
     * potentially slow on the very first call (the host boots the engine + graphics device), so call this
     * off the EDT.
     */
    fun render(text: String, width: Int, height: Int): RenderResult {
        synchronized(lock) {
            return try {
                ensureProcess()
                val request = buildRenderRequest(text, width, height)
                writer!!.apply { write(request); write("\n"); flush() }
                val line = reader!!.readLine() ?: throw RuntimeException("designer host closed the connection")
                parseResponse(line)
            } catch (e: Exception) {
                stopProcess() // force a clean restart on the next render
                RenderResult(null, e.message ?: e.toString(), emptyList())
            }
        }
    }

    private fun ensureProcess() {
        process?.let { if (it.isAlive) return }
        stopProcess()

        val exe = resolveHostExecutable()
        val command = GeneralCommandLine(exe.toString(), "serve").apply { charset = StandardCharsets.UTF_8 }
        val proc = command.createProcess()

        process = proc
        writer = BufferedWriter(OutputStreamWriter(proc.outputStream, StandardCharsets.UTF_8))
        reader = BufferedReader(InputStreamReader(proc.inputStream, StandardCharsets.UTF_8))
        drainStderr(proc)
    }

    private fun stopProcess() {
        try { writer?.close() } catch (_: Exception) {}
        try { reader?.close() } catch (_: Exception) {}
        process?.destroy()
        writer = null
        reader = null
        process = null
    }

    private fun resolveHostExecutable(): Path {
        val override = System.getenv(HOST_ENV)?.takeIf { it.isNotBlank() }
            ?: error(
                "Set the $HOST_ENV environment variable to the built designer host, e.g. " +
                    "…/output/Adamantium.UI.Designer.Host/bin/x64/Debug/net10.0/Adamantium.UI.Designer.Host.exe"
            )
        val path = Path.of(override)
        if (!path.exists()) error("$HOST_ENV points to a missing file: $override")
        return path
    }

    /** Engine logs go to the host's stderr; drain it so a full pipe can't block the process. */
    private fun drainStderr(proc: Process) {
        Thread {
            try {
                proc.errorStream.bufferedReader().forEachLine { LOG.debug(it) }
            } catch (_: Exception) {
                // process gone - nothing to drain
            }
        }.apply { isDaemon = true; name = "auml-designer-host-stderr" }.start()
    }

    override fun dispose() = synchronized(lock) { stopProcess() }

    // --- protocol -------------------------------------------------------------------------------------

    private fun buildRenderRequest(text: String, width: Int, height: Int): String =
        """{"op":"render","text":"${jsonEscape(text)}","width":$width,"height":$height}"""

    private fun parseResponse(line: String): RenderResult {
        val obj = MiniJson.parse(line) as? Map<*, *>
            ?: return RenderResult(null, "unexpected host response: $line", emptyList())
        val diagnostics = (obj["diagnostics"] as? List<*>)?.filterIsInstance<String>() ?: emptyList()
        return RenderResult(obj["png"] as? String, obj["error"] as? String, diagnostics)
    }

    private fun jsonEscape(s: String): String {
        val sb = StringBuilder(s.length + 16)
        for (c in s) when (c) {
            '"' -> sb.append("\\\"")
            '\\' -> sb.append("\\\\")
            '\n' -> sb.append("\\n")
            '\r' -> sb.append("\\r")
            '\t' -> sb.append("\\t")
            '\b' -> sb.append("\\b")
            12.toChar() -> sb.append("\\f")
            else -> if (c < ' ') sb.append("\\u%04x".format(c.code)) else sb.append(c)
        }
        return sb.toString()
    }

    companion object {
        private const val HOST_ENV = "ADAMANTIUM_DESIGNER_HOST"
        private val LOG = logger<AumlPreviewService>()
    }
}

/** Tiny dependency-free JSON reader - enough for the host's flat `{png|error|diagnostics}` responses. */
private object MiniJson {
    fun parse(text: String): Any? = Parser(text).value()

    private class Parser(private val s: String) {
        private var i = 0

        fun value(): Any? {
            skipWs()
            return when (s[i]) {
                '{' -> obj()
                '[' -> arr()
                '"' -> str()
                't' -> { i += 4; true }
                'f' -> { i += 5; false }
                'n' -> { i += 4; null }
                else -> num()
            }
        }

        private fun obj(): Map<String, Any?> {
            val map = LinkedHashMap<String, Any?>()
            i++ // {
            skipWs()
            if (s[i] == '}') { i++; return map }
            while (true) {
                skipWs()
                val key = str()
                skipWs(); i++ // :
                map[key] = value()
                skipWs()
                if (s[i] == ',') i++ else { i++; break } // , or }
            }
            return map
        }

        private fun arr(): List<Any?> {
            val list = ArrayList<Any?>()
            i++ // [
            skipWs()
            if (s[i] == ']') { i++; return list }
            while (true) {
                list.add(value())
                skipWs()
                if (s[i] == ',') i++ else { i++; break } // , or ]
            }
            return list
        }

        private fun str(): String {
            val sb = StringBuilder()
            i++ // opening quote
            while (s[i] != '"') {
                val c = s[i++]
                if (c == '\\') {
                    when (val e = s[i++]) {
                        '"' -> sb.append('"')
                        '\\' -> sb.append('\\')
                        '/' -> sb.append('/')
                        'n' -> sb.append('\n')
                        't' -> sb.append('\t')
                        'r' -> sb.append('\r')
                        'b' -> sb.append('\b')
                        'f' -> sb.append(12.toChar())
                        'u' -> { sb.append(s.substring(i, i + 4).toInt(16).toChar()); i += 4 }
                        else -> sb.append(e)
                    }
                } else {
                    sb.append(c)
                }
            }
            i++ // closing quote
            return sb.toString()
        }

        private fun num(): Double {
            val start = i
            while (i < s.length && (s[i].isDigit() || s[i] in "-+.eE")) i++
            return s.substring(start, i).toDouble()
        }

        private fun skipWs() { while (i < s.length && s[i].isWhitespace()) i++ }
    }
}
