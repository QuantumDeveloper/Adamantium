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
    // The sourcePath of the last successful render = the owner of the host's single warm scene. Only that preview may
    // stream frames; a different (background) preview's frame() would advance and return THIS scene - i.e. show the
    // wrong file. Reset whenever the host process restarts.
    private var lastRenderPath: String? = null

    /**
     * Result of one render: a PNG path (+ its pixel size) on success, or an error; diagnostics are non-fatal
     * notes either way. [width]/[height] are the rendered image's size (the window's design size × scale).
     */
    data class RenderResult(
        // The current frame file path(s), RAW B8G8R8A8 (width*height*4 bytes, no encode). A single-element list; the
        // editor converts the bytes itself. Animations are streamed: while [animating], call frame() for the next one.
        val frames: List<String>,
        val error: String?,
        val diagnostics: List<String>,
        val width: Int?,
        val height: Int?,
        val scale: Double?,
        // The window's authored design size (before scale). Used for the size readout so it shows the true size
        // instead of pixelSize / scale, which loses ±1px at fractional (auto-fit) scales. Null from older hosts.
        val designWidth: Int? = null,
        val designHeight: Int? = null,
        // True while the live scene still has something to animate. The editor keeps requesting the next frame (frame())
        // at ~60fps while this is true, then stops - a live stream rather than a pre-captured sequence.
        val animating: Boolean = false,
    )

    /** A designer hit-test result: the authored element's markup position (1-based line/column) and its rect in
     * the frame's design space (for the hover frame). */
    data class HitResult(
        val line: Int,
        val column: Int,
        val x: Double,
        val y: Double,
        val width: Double,
        val height: Double,
    )

    /** Result of a designer selection (op "select"): the re-rendered frame WITH the framework's selection frame drawn
     * on it ([render]) and the selected element's markup position for editor caret sync ([hit]; null = nothing selected,
     * i.e. the selection was cleared). */
    data class SelectResult(val render: RenderResult, val hit: HitResult?)

    /**
     * Renders [text] at the window's design size × [scale]. A settled render returns one frame; a [live] render returns
     * the whole animation sequence (the host renders it all in this one call). Blocking and potentially slow on the very
     * first call (the host boots the engine + graphics device), so call this off the EDT.
     */
    fun render(text: String, scale: Double, sourcePath: String? = null, live: Boolean = false): RenderResult {
        synchronized(lock) {
            return try {
                ensureProcess()
                val request = buildRenderRequest(text, scale, sourcePath, live)
                writer!!.apply { write(request); write("\n"); flush() }
                val line = reader!!.readLine() ?: throw RuntimeException("designer host closed the connection")
                val result = parseResponse(line)
                // A successful render makes this preview the owner of the host's single warm scene (see frame()).
                if (result.error == null) lastRenderPath = sourcePath
                result
            } catch (e: Exception) {
                stopProcess() // force a clean restart on the next render
                RenderResult(emptyList(), e.message ?: e.toString(), emptyList(), null, null, null)
            }
        }
    }

    /**
     * Advances the live preview by one frame (op "frame") and returns it. The editor calls this in a loop (off the EDT)
     * while [RenderResult.animating] is true to play design-time animations in real time. Shares [lock] with [render]
     * so a new render and the streaming loop never interleave on the host's stdio.
     */
    fun frame(ownerPath: String?): RenderResult {
        synchronized(lock) {
            // Only the most-recently-rendered preview owns the host's single warm scene. If a different (background)
            // preview asks for a frame, advancing the host would hand it THIS scene's frame (the wrong file), so report
            // "not animating" - its stream stops and it freezes on its last frame until it is re-rendered (re-owns).
            if (ownerPath != null && lastRenderPath != null && ownerPath != lastRenderPath) {
                return RenderResult(emptyList(), null, emptyList(), null, null, null, animating = false)
            }
            return try {
                ensureProcess()
                writer!!.apply { write("{\"op\":\"frame\"}"); write("\n"); flush() }
                val line = reader!!.readLine() ?: throw RuntimeException("designer host closed the connection")
                parseResponse(line)
            } catch (e: Exception) {
                stopProcess() // force a clean restart on the next render
                RenderResult(emptyList(), e.message ?: e.toString(), emptyList(), null, null, null)
            }
        }
    }

    /**
     * Selects the authored element at a point in the last render's design space: the host drives the framework
     * AdornerLayer (so the FRAMEWORK draws the selection frame from the element's stroke-aware RenderBounds) and
     * re-renders, returning the updated frame plus the element's markup line/column. A miss clears the selection.
     * Must follow a [render]; off-EDT; on error returns an error RenderResult and a null hit.
     */
    fun select(x: Double, y: Double): SelectResult {
        synchronized(lock) {
            return try {
                ensureProcess()
                writer!!.apply { write("{\"op\":\"select\",\"x\":$x,\"y\":$y}"); write("\n"); flush() }
                val line = reader!!.readLine() ?: throw RuntimeException("designer host closed the connection")
                val render = parseResponse(line)
                val obj = MiniJson.parse(line) as? Map<*, *>
                val hit = (obj?.get("hit") as? Map<*, *>)?.let { h ->
                    val ln = (h["line"] as? Double)?.toInt() ?: return@let null
                    HitResult(ln, (h["column"] as? Double)?.toInt() ?: 0,
                        h["x"] as? Double ?: 0.0, h["y"] as? Double ?: 0.0,
                        h["width"] as? Double ?: 0.0, h["height"] as? Double ?: 0.0)
                }
                SelectResult(render, hit)
            } catch (e: Exception) {
                stopProcess()
                SelectResult(RenderResult(emptyList(), e.message ?: e.toString(), emptyList(), null, null, null), null)
            }
        }
    }

    /**
     * Hovers the element at a point in the last render's design space: the host drives the framework AdornerLayer hover
     * (so the FRAMEWORK draws the stroke-aware hover frame) and re-renders, returning the updated frame. Null coords
     * clear the hover frame. Must follow a [render]; off-EDT; on error returns an error RenderResult.
     */
    fun hover(x: Double?, y: Double?): RenderResult {
        synchronized(lock) {
            return try {
                ensureProcess()
                val coords = if (x != null && y != null) ",\"x\":$x,\"y\":$y" else ""
                writer!!.apply { write("{\"op\":\"hover\"$coords}"); write("\n"); flush() }
                val line = reader!!.readLine() ?: throw RuntimeException("designer host closed the connection")
                parseResponse(line)
            } catch (e: Exception) {
                stopProcess()
                RenderResult(emptyList(), e.message ?: e.toString(), emptyList(), null, null, null)
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
        lastRenderPath = null   // the warm scene died with the process; the next render re-establishes ownership
    }

    private fun resolveHostExecutable(): Path {
        val override = System.getenv(HOST_ENV)?.takeIf { it.isNotBlank() }
            ?: error(
                "Set the $HOST_ENV environment variable to the built designer host, e.g. " +
                    "…/artifacts/designer-host/Debug/net10.0/Adamantium.UI.Designer.Host.exe"
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

    // sourcePath (the edited .auml's path) lets the host resolve relative asset paths (e.g. an Image Source) against
    // the file's project root, so the preview loads those assets from source just like the running app does.
    private fun buildRenderRequest(text: String, scale: Double, sourcePath: String?, live: Boolean): String {
        val uriPart = if (sourcePath.isNullOrBlank()) "" else ",\"uri\":\"${jsonEscape(sourcePath)}\""
        val livePart = if (live) ",\"live\":true" else ""
        return "{\"op\":\"render\",\"text\":\"${jsonEscape(text)}\",\"scale\":$scale$uriPart$livePart}"
    }

    private fun parseResponse(line: String): RenderResult {
        val obj = MiniJson.parse(line) as? Map<*, *>
            ?: return RenderResult(emptyList(), "unexpected host response: $line", emptyList(), null, null, null)
        val diagnostics = (obj["diagnostics"] as? List<*>)?.filterIsInstance<String>() ?: emptyList()
        val frames = (obj["frames"] as? List<*>)?.filterIsInstance<String>() ?: emptyList()
        val width = (obj["width"] as? Double)?.toInt()
        val height = (obj["height"] as? Double)?.toInt()
        val scale = obj["scale"] as? Double
        val designWidth = (obj["designWidth"] as? Double)?.toInt()
        val designHeight = (obj["designHeight"] as? Double)?.toInt()
        val animating = obj["animating"] as? Boolean ?: false
        return RenderResult(frames, obj["error"] as? String, diagnostics, width, height, scale, designWidth, designHeight, animating)
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
