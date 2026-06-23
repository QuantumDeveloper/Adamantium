package com.adamantium.auml

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.application.ModalityState
import com.intellij.openapi.editor.Document
import com.intellij.openapi.editor.event.DocumentEvent
import com.intellij.openapi.editor.event.DocumentListener
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.FileEditor
import com.intellij.openapi.fileEditor.FileEditorLocation
import com.intellij.openapi.fileEditor.FileEditorState
import com.intellij.openapi.fileEditor.FileEditorStateLevel
import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.ComboBox
import com.intellij.openapi.ui.popup.JBPopupFactory
import com.intellij.openapi.util.UserDataHolderBase
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.ui.JBColor
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBScrollPane
import com.intellij.util.Alarm
import com.intellij.util.ui.JBUI
import java.awt.BorderLayout
import java.awt.Dimension
import java.awt.FlowLayout
import java.awt.Point
import java.awt.Graphics
import java.awt.Graphics2D
import java.awt.Rectangle
import java.awt.RenderingHints
import java.awt.event.ComponentAdapter
import java.awt.event.ComponentEvent
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.awt.event.MouseMotionAdapter
import java.awt.geom.Point2D
import java.awt.image.BufferedImage
import java.beans.PropertyChangeListener
import javax.swing.BorderFactory
import javax.swing.JButton
import javax.swing.JComponent
import javax.swing.JLayeredPane
import javax.swing.JToggleButton
import javax.swing.JPanel
import javax.swing.JViewport
import javax.swing.Scrollable
import kotlin.math.abs
import kotlin.math.max
import kotlin.math.roundToInt

/**
 * Read-only preview half of the AUML split editor: renders the current editor buffer through the designer
 * host and paints the result on a checkerboard canvas. Re-renders live on edit (debounced) and on zoom change
 * - zooming asks the host to re-render at design size × scale, so text/geometry stay crisp (the old frame is
 * scaled in place for instant feedback until the sharp one arrives). View-only: zoom + pan, no input forwarding.
 */
class AumlPreviewFileEditor(
    private val project: Project,
    private val file: VirtualFile,
) : UserDataHolderBase(), FileEditor {

    private val service = project.getService(AumlPreviewService::class.java)
    private val document: Document? = FileDocumentManager.getInstance().getDocument(file)
    private val alarm = Alarm(Alarm.ThreadToUse.SWING_THREAD, this)
    // Hover hit-tests are throttled off-EDT so moving the mouse doesn't flood the designer host with requests.
    private val hoverAlarm = Alarm(Alarm.ThreadToUse.POOLED_THREAD, this)
    // Bumped by every render (and dispose) so a stale render/streaming loop from a previous request supersedes itself:
    // the streaming loop exits as soon as it sees renderToken has moved on. Written on the EDT, read by the streaming
    // loop on a pooled thread -> @Volatile so the loop sees a new render promptly.
    @Volatile private var renderToken = 0

    private var scale = 1.0
    private var updatingZoom = false
    private var autoFit = true   // keep the frame fitted to the viewport until the user zooms manually

    private val canvas = ZoomableCanvas()
    private val zoomCombo = ComboBox(ZOOM_PRESETS.map { percent(it) }.toTypedArray()).apply {
        isEditable = true
        selectedItem = percent(1.0)
        addActionListener { if (!updatingZoom) (selectedItem as? String)?.let(::applyZoomText) }
    }
    // Fatal render/host errors (host missing, render failure) surface as a compact red badge floating over the
    // canvas — drag to move, click for the full message in a popup (mirrors the diagnostics badge below). Never
    // sits in the toolbar.
    private var currentError: String? = null
    private var errorBadgePos: Point? = null
    private var errorBadgeDragOffset: Point? = null
    private var errorBadgeDragged = false
    private val errorBadge = JBLabel("⚠ designer error").apply {
        foreground = JBColor.RED
        isOpaque = true
        background = JBColor.background()
        border = BorderFactory.createCompoundBorder(
            BorderFactory.createLineBorder(JBColor.RED, 1, true),
            JBUI.Borders.empty(2, 6))
        isVisible = false
        cursor = java.awt.Cursor.getPredefinedCursor(java.awt.Cursor.MOVE_CURSOR)
        toolTipText = "Designer error — drag to move, click for details"
        val handler = object : java.awt.event.MouseAdapter() {
            override fun mousePressed(e: java.awt.event.MouseEvent) { errorBadgeDragOffset = e.point; errorBadgeDragged = false }
            override fun mouseDragged(e: java.awt.event.MouseEvent) {
                val off = errorBadgeDragOffset ?: return
                errorBadgeDragged = true
                errorBadgePos = Point(x + e.x - off.x, y + e.y - off.y)
                layoutOverlay()
            }
            override fun mouseReleased(e: java.awt.event.MouseEvent) {
                if (!errorBadgeDragged) showErrorPopup()
                errorBadgeDragOffset = null
            }
        }
        addMouseListener(handler)
        addMouseMotionListener(handler)
    }
    private val sizeLabel = JBLabel().apply { border = JBUI.Borders.emptyLeft(8) }
    // Non-fatal designer diagnostics (e.g. "Cannot convert '75,75' to Vector2", unresolved behavior): the preview
    // still renders, but the user must see what silently didn't apply. Kept as a compact "⚠ N" badge (fixed,
    // tiny width so it never blocks the preview from shrinking); click it for the full list in a popup.
    private var currentDiagnostics: List<String> = emptyList()
    // Floating "⚠ N" badge over the canvas: drag to reposition, click (without dragging) opens the full list in a
    // popup. Lives on canvasLayer (not the toolbar) so it floats over the preview and never affects layout width.
    private var badgePos: Point? = null         // user-dragged position; null = default top-right corner
    private var badgeDragOffset: Point? = null  // press point within the badge while dragging
    private var badgeDragged = false
    private val diagBadge = JBLabel().apply {
        foreground = JBColor.ORANGE
        isOpaque = true
        background = JBColor.background()
        border = BorderFactory.createCompoundBorder(
            BorderFactory.createLineBorder(JBColor.ORANGE, 1, true),
            JBUI.Borders.empty(2, 6))
        isVisible = false
        cursor = java.awt.Cursor.getPredefinedCursor(java.awt.Cursor.MOVE_CURSOR)
        toolTipText = "Designer diagnostics — drag to move, click for details"
        val handler = object : java.awt.event.MouseAdapter() {
            override fun mousePressed(e: java.awt.event.MouseEvent) { badgeDragOffset = e.point; badgeDragged = false }
            override fun mouseDragged(e: java.awt.event.MouseEvent) {
                val off = badgeDragOffset ?: return
                badgeDragged = true
                badgePos = Point(x + e.x - off.x, y + e.y - off.y)
                layoutOverlay()
            }
            override fun mouseReleased(e: java.awt.event.MouseEvent) {
                if (!badgeDragged) showDiagnosticsPopup()
                badgeDragOffset = null
            }
        }
        addMouseListener(handler)
        addMouseMotionListener(handler)
    }
    // Stays "pressed" while auto-fit is tracking the window; pops out when you zoom manually — a visible
    // indicator of the auto-fit state. Clicking it always (re-)fits and turns tracking back on.
    private val fitToggle = JToggleButton("Fit").apply {
        toolTipText = "Fit to window — stays on until you zoom manually"
        isSelected = true
        addActionListener { fitNow() }
    }
    private val scrollPane = JBScrollPane(canvas).apply { isWheelScrollingEnabled = false }
    // Layered host: the scroll pane fills the area, the diagnostics badge floats on top of it. doLayout (run on
    // every validation/resize) keeps the scroll pane filled and the badge positioned.
    private val canvasLayer = object : JLayeredPane() {
        override fun doLayout() = layoutOverlay()
    }.apply {
        add(scrollPane, JLayeredPane.DEFAULT_LAYER as Any?)
        add(diagBadge, JLayeredPane.PALETTE_LAYER as Any?)
        add(errorBadge, JLayeredPane.PALETTE_LAYER as Any?)
    }
    private val root = JPanel(BorderLayout()).apply {
        add(canvasLayer, BorderLayout.CENTER)
        add(buildToolbar(), BorderLayout.SOUTH)
    }

    init {
        document?.addDocumentListener(object : DocumentListener {
            // Edits render LIVE (stream): the host returns one frame fast (no batch capture, no multi-second freeze),
            // then the editor streams the animation frame-by-frame, so animations keep playing after a change.
            override fun documentChanged(event: DocumentEvent) = scheduleRender()
        }, this)
        scrollPane.addMouseWheelListener { e ->
            if (e.isControlDown) {
                setScale(scale * if (e.wheelRotation < 0) ZOOM_STEP else 1.0 / ZOOM_STEP)
            } else {
                val bar = if (e.isShiftDown) scrollPane.horizontalScrollBar else scrollPane.verticalScrollBar
                bar.value += e.wheelRotation * bar.unitIncrement * 3
            }
        }
        // While auto-fit is on, keep the frame fitted as the panel is resized.
        scrollPane.viewport.addComponentListener(object : ComponentAdapter() {
            override fun componentResized(e: ComponentEvent) { if (autoFit) applyFit() }
        })
        // Designer interaction: hover highlights the element under the cursor (cheap plugin hint); click SELECTS it -
        // the framework draws the stroke-aware selection frame into the rendered frame - and jumps to its markup.
        canvas.addMouseMotionListener(object : MouseMotionAdapter() {
            override fun mouseMoved(e: MouseEvent) = scheduleHover(e.point)
        })
        canvas.addMouseListener(object : MouseAdapter() {
            override fun mouseClicked(e: MouseEvent) = navigateAt(e.point)
            override fun mouseExited(e: MouseEvent) = scheduleHover(null)   // clear the framework hover frame
        })
        scheduleRender(live = true)   // play the design-time animations on first load
    }

    // Hover frame (FRAMEWORK-drawn): throttle (one per ~60 ms of movement), then ask the host to hover the element under
    // the point - the framework re-renders the frame with its stroke-aware hover adorner - and show that frame. A null
    // point (or off the rendered frame) clears the hover. The host returns the deepest element, so moving onto a child
    // of a large container switches the hover to the child.
    private fun scheduleHover(p: Point?) {
        val design = p?.let { canvas.canvasToDesign(it) }
        val token = renderToken          // captured on the EDT; a later render/select supersedes this hover frame
        val renderScale = scale
        hoverAlarm.cancelAllRequests()
        hoverAlarm.addRequest({ requestHover(design?.x, design?.y, token, renderScale) }, HOVER_DEBOUNCE_MS)
    }

    // Off-EDT (hoverAlarm pool): drive the framework hover frame and show the re-rendered result. Does NOT bump
    // renderToken (so it can't kill a running animation stream); discards its frame if a render/select happened meanwhile.
    private fun requestHover(x: Double?, y: Double?, token: Int, renderScale: Double) {
        val r = service.hover(x, y)
        val w = r.width ?: 0
        val h = r.height ?: 0
        val image = r.frames.firstOrNull()?.let { loadRawBgra(it, w, h) } ?: return
        ApplicationManager.getApplication().invokeLater({
            if (token != renderToken) return@invokeLater
            canvas.setImage(image, r.scale ?: renderScale)
            canvas.setStale(false)
        }, ModalityState.any())
    }

    // Click -> select the element: the host drives the framework AdornerLayer so the FRAMEWORK draws the (stroke-aware)
    // selection frame INTO the rendered frame (not a plugin-side rect); show that frame and jump the text editor (the
    // split's code half) to the element's markup line/column. Clicking empty space clears the selection.
    private fun navigateAt(p: Point) {
        val design = canvas.canvasToDesign(p) ?: return
        val token = ++renderToken   // a select re-renders; supersede any running stream so it doesn't fight the select frame
        stopPlayback()
        val renderScale = scale
        ApplicationManager.getApplication().executeOnPooledThread {
            val result = service.select(design.x, design.y)
            val r = result.render
            val w = r.width ?: 0
            val h = r.height ?: 0
            val image = r.frames.firstOrNull()?.let { loadRawBgra(it, w, h) }
            ApplicationManager.getApplication().invokeLater({
                if (token != renderToken) return@invokeLater   // superseded by a newer render/select
                if (image != null) {
                    canvas.setImage(image, r.scale ?: renderScale)
                    canvas.setStale(false)
                }
                result.hit?.let { hit ->
                    OpenFileDescriptor(project, file, (hit.line - 1).coerceAtLeast(0), (hit.column - 1).coerceAtLeast(0)).navigate(true)
                }
                if (r.animating) startStreaming(token, renderScale)
            }, ModalityState.any())
        }
    }

    private fun buildToolbar(): JComponent = JPanel(FlowLayout(FlowLayout.LEFT, 4, 2)).apply {
        add(JButton("−").apply { addActionListener { setScale(scale / ZOOM_STEP) } })
        add(zoomCombo)
        add(JButton("+").apply { addActionListener { setScale(scale * ZOOM_STEP) } })
        add(JButton("100%").apply { addActionListener { setScale(1.0) } })
        add(fitToggle)
        add(JButton("⟳").apply { toolTipText = "Re-render & replay animations"; addActionListener { renderNow(announce = true, live = true) } })
        add(JButton("Bg").apply { toolTipText = "Background: checkerboard / dark / light"; addActionListener { canvas.cycleBackground() } })
        add(sizeLabel)
    }

    /** Fit button: fit the whole frame to the window AND re-enable auto-fit so it keeps tracking the window. */
    private fun fitNow() {
        autoFit = true
        fitToggle.isSelected = true   // clicking when already on (Swing toggled it off) keeps it visibly on
        applyFit()
    }

    /** Auto-fit: fit the frame to the viewport by adjusting ONLY the display scale - the canvas scales the shown frame
     * (downscaling to fit stays crisp). It never schedules a re-render, so it can't bump the render token or interrupt
     * an animation playback. (Manual zoom, via setScale, DOES re-render at the new scale - crisp when zooming in.) */
    private fun applyFit() {
        val target = computeFitScale() ?: return
        if (abs(target - scale) <= 0.005) return
        scale = target
        canvas.displayScale = scale
        updatingZoom = true
        zoomCombo.selectedItem = percent(scale)
        updatingZoom = false
    }

    /** Scale that fits the whole rendered frame into the viewport (with a small margin), clamped; null if empty. */
    private fun computeFitScale(): Double? {
        val design = canvas.designSize() ?: return null
        val view = scrollPane.viewport.extentSize
        if (design.width <= 0 || design.height <= 0 || view.width <= 0 || view.height <= 0) return null
        val margin = 8   // a little breathing room so the frame isn't flush against the edges
        val fit = minOf((view.width - margin).toDouble() / design.width,
                        (view.height - margin).toDouble() / design.height)
        return fit.coerceIn(MIN_SCALE, MAX_SCALE)
    }

    private fun applyZoomText(text: String) {
        val value = text.trim().removeSuffix("%").trim().replace(',', '.').toDoubleOrNull() ?: return
        setScale(value / 100.0)
    }

    private fun setScale(newScale: Double, userInitiated: Boolean = true) {
        if (userInitiated) { autoFit = false; fitToggle.isSelected = false }   // manual zoom stops (and un-presses) auto-fit
        scale = newScale.coerceIn(MIN_SCALE, MAX_SCALE)
        canvas.displayScale = scale // instant feedback: scales the current frame until the re-render lands
        updatingZoom = true
        zoomCombo.selectedItem = percent(scale)
        updatingZoom = false
        scheduleRender()
    }

    // live=true renders one frame and then streams the design-time animations (first load, ⟳, edits, zoom). Bumping the
    // token here makes the current streaming loop exit at once, rather than letting it run out the debounce.
    private fun scheduleRender(live: Boolean = true) {
        if (document == null) return
        renderToken++
        stopPlayback()
        alarm.cancelAllRequests()
        alarm.addRequest({ renderNow(live = live) }, DEBOUNCE_MS)
    }

    /**
     * On the EDT: snapshot text + scale, then render off-thread and apply the result back on the EDT.
     * announce=true (manual ⟳) shows a transient "rendering…" so the click visibly takes effect even when
     * the markup is unchanged and the resulting frame looks identical.
     */
    private fun renderNow(announce: Boolean = false, live: Boolean = true) {
        val doc = document ?: return
        // A fresh render supersedes any running stream (a later edit/zoom/replay render stops the previous one).
        val token = ++renderToken
        stopPlayback()
        if (announce) { sizeLabel.text = "rendering…"; hideError() }
        val text = doc.text
        val renderScale = scale
        ApplicationManager.getApplication().executeOnPooledThread {
            val result = service.render(text, renderScale, file.path, live)
            val w = result.width ?: 0
            val h = result.height ?: 0
            val image = result.frames.firstOrNull()?.let { loadRawBgra(it, w, h) }
            ApplicationManager.getApplication().invokeLater({
                if (token != renderToken) return@invokeLater   // superseded by a newer render
                applyResult(result, image, renderScale)
                if (result.animating) startStreaming(token, renderScale)
            }, ModalityState.any())
        }
    }

    // Streams the live animation: requests the next frame from the host in a loop (off the EDT) and shows each as it
    // arrives, until the host reports it is no longer animating or a newer render supersedes this one (token). Unlike
    // the old batch playback, the scene stays live on the host, so an edit just keeps animating instead of re-capturing
    // a whole sequence (no freeze) and a one-shot transition isn't restarted by an unrelated change.
    private fun startStreaming(token: Int, renderScale: Double) {
        ApplicationManager.getApplication().executeOnPooledThread {
            while (token == renderToken) {
                val r = service.frame(file.path)
                if (token != renderToken || r.error != null) break
                val w = r.width ?: 0
                val h = r.height ?: 0
                val img = r.frames.firstOrNull()?.let { loadRawBgra(it, w, h) }
                val frameScale = r.scale ?: renderScale
                if (img != null) {
                    ApplicationManager.getApplication().invokeLater({
                        if (token == renderToken) { canvas.setImage(img, frameScale); canvas.setStale(false) }
                    }, ModalityState.any())
                }
                if (!r.animating) break
            }
        }
    }

    // A newer render bumps renderToken, which makes any running streaming loop exit on its next check; nothing else to stop.
    private fun stopPlayback() {
    }

    // Reads a raw B8G8R8A8 frame (width*height*4 bytes, native render-target layout) and packs it into an ARGB image.
    // Allocates per call (no shared buffers) so a concurrent edit-render and animation-tick can't race on a frame.
    private fun loadRawBgra(path: String, w: Int, h: Int): BufferedImage? {
        val need = w * h * 4
        val buf = ByteArray(need)
        val ok = runCatching {
            java.io.FileInputStream(path).use { fis ->
                var off = 0
                while (off < need) {
                    val n = fis.read(buf, off, need - off)
                    if (n < 0) return@runCatching false
                    off += n
                }
                true
            }
        }.getOrDefault(false)
        if (!ok) return null

        val img = BufferedImage(w, h, BufferedImage.TYPE_INT_ARGB)
        val argb = (img.raster.dataBuffer as java.awt.image.DataBufferInt).data
        var p = 0
        for (i in 0 until w * h) {
            argb[i] = ((buf[p + 3].toInt() and 0xFF) shl 24) or   // A
                      ((buf[p + 2].toInt() and 0xFF) shl 16) or   // R
                      ((buf[p + 1].toInt() and 0xFF) shl 8) or    // G
                      (buf[p].toInt() and 0xFF)                   // B
            p += 4
        }
        return img
    }

    private fun applyResult(result: AumlPreviewService.RenderResult, image: BufferedImage?, requestedScale: Double) {
        showDiagnostics(result.diagnostics)
        if (image != null) {
            // The host may render below the requested scale (size cap); use the scale it actually rendered at so
            // the canvas upscales to the requested zoom. Updating only on success keeps the last good frame.
            val frameScale = result.scale ?: requestedScale
            canvas.setImage(image, frameScale)
            canvas.setStale(false)   // fresh frame: undim
            // Prefer the host's authored design size; only fall back to reconstructing it from the scaled pixels
            // (which loses ±1px at fractional auto-fit scales) for older hosts that don't report it.
            val labelW = result.designWidth ?: (image.width / frameScale).roundToInt()
            val labelH = result.designHeight ?: (image.height / frameScale).roundToInt()
            sizeLabel.text = "$labelW × $labelH px"
            hideError()
            // Auto-fit: snap to fit. applyFit no-ops once already at fit (can't loop re-rendering) and, while an
            // animation is playing, only rescales the display (no re-render) so it can't interrupt the animation.
            if (autoFit) applyFit()
        } else {
            // Keep the last good frame (don't clear it) but dim it, and surface the error as a non-blocking badge.
            canvas.setStale(true)
            showError(result.error ?: "render failed")
        }
    }

    // Surface non-fatal designer diagnostics as a compact "⚠ N" badge floating over the canvas (click for the
    // full list). Floating (not inline) so it never affects the toolbar width / window minimum size.
    private fun showDiagnostics(diagnostics: List<String>) {
        currentDiagnostics = diagnostics
        diagBadge.isVisible = diagnostics.isNotEmpty()
        if (diagnostics.isNotEmpty()) diagBadge.text = "⚠ ${diagnostics.size}"
        layoutOverlay()
    }

    // Fatal errors float as a draggable red badge (click for the full message) instead of sitting in the toolbar.
    private fun showError(message: String) {
        currentError = message
        errorBadge.isVisible = true
        layoutOverlay()
    }

    private fun hideError() {
        if (errorBadge.isVisible) { errorBadge.isVisible = false; layoutOverlay() }
    }

    private fun showErrorPopup() {
        val msg = currentError ?: return
        val area = javax.swing.JTextArea(msg).apply {
            isEditable = false
            lineWrap = true
            wrapStyleWord = true
            border = JBUI.Borders.empty(8)
        }
        val scroll = JBScrollPane(area).apply { preferredSize = Dimension(560, 160) }
        JBPopupFactory.getInstance()
            .createComponentPopupBuilder(scroll, area)
            .setTitle("Designer error")
            .setResizable(true)
            .setMovable(true)
            .setRequestFocus(true)
            .createPopup()
            .showUnderneathOf(errorBadge)
    }

    // Keeps the scroll pane filling canvasLayer and the floating badge sized/positioned (default top-right, or the
    // user's dragged position, always clamped inside the visible area).
    private fun layoutOverlay() {
        scrollPane.setBounds(0, 0, canvasLayer.width, canvasLayer.height)
        if (errorBadge.isVisible) {
            val d = errorBadge.preferredSize
            val p = errorBadgePos ?: Point(10, 8)
            val cx = p.x.coerceIn(0, max(0, canvasLayer.width - d.width))
            val cy = p.y.coerceIn(0, max(0, canvasLayer.height - d.height))
            errorBadge.setBounds(cx, cy, d.width, d.height)
        }
        if (diagBadge.isVisible) {
            val d = diagBadge.preferredSize
            val p = badgePos ?: Point(canvasLayer.width - d.width - 10, 8)
            val cx = p.x.coerceIn(0, max(0, canvasLayer.width - d.width))
            val cy = p.y.coerceIn(0, max(0, canvasLayer.height - d.height))
            diagBadge.setBounds(cx, cy, d.width, d.height)
        }
    }

    private fun showDiagnosticsPopup() {
        if (currentDiagnostics.isEmpty()) return
        val area = javax.swing.JTextArea(currentDiagnostics.joinToString("\n\n")).apply {
            isEditable = false
            lineWrap = true
            wrapStyleWord = true
            border = JBUI.Borders.empty(8)
        }
        val scroll = JBScrollPane(area).apply { preferredSize = Dimension(520, 220) }
        JBPopupFactory.getInstance()
            .createComponentPopupBuilder(scroll, area)
            .setTitle("Designer diagnostics (${currentDiagnostics.size})")
            .setResizable(true)
            .setMovable(true)
            .setRequestFocus(true)
            .createPopup()
            .showUnderneathOf(diagBadge)
    }

    override fun getComponent(): JComponent = root
    override fun getPreferredFocusedComponent(): JComponent = canvas
    override fun getName(): String = "Preview"
    override fun getFile(): VirtualFile = file
    override fun setState(state: FileEditorState) {}

    // The shared designer host keeps ONE warm scene = the last preview rendered, and only its owner streams frames.
    // Re-render when this preview is (re)selected so it re-acquires that ownership and its live animation resumes
    // after another preview took over - otherwise a backgrounded preview would stay frozen on its last frame.
    override fun selectNotify() { scheduleRender() }
    override fun getState(level: FileEditorStateLevel): FileEditorState = FileEditorState.INSTANCE
    override fun isModified(): Boolean = false
    override fun isValid(): Boolean = true
    override fun getCurrentLocation(): FileEditorLocation? = null
    override fun addPropertyChangeListener(listener: PropertyChangeListener) {}
    override fun removePropertyChangeListener(listener: PropertyChangeListener) {}
    override fun dispose() { renderToken++; stopPlayback() }   // stop any running animation playback

    /**
     * Paints the rendered frame on a checkerboard, scaled by displayScale/renderedScale (so a zoom shows the
     * current frame scaled instantly, then 1:1 once the host re-renders at the new scale). Fills the viewport
     * when the frame is smaller (checkerboard around it), scrolls when larger.
     */
    private class ZoomableCanvas : JComponent(), Scrollable {
        private var image: BufferedImage? = null
        private var renderedScale = 1.0
        var displayScale = 1.0
            set(value) { field = value; revalidate(); repaint() }

        fun setImage(img: BufferedImage, scale: Double) {
            // Do NOT touch displayScale here: it's the user's current zoom and may have moved on while this
            // render was in flight. Overwriting it makes a late frame yank the view back to a stale scale.
            image = img
            renderedScale = scale
            revalidate()
            repaint()
        }

        // When a render fails (transiently invalid markup mid-edit), the last good frame is kept but dimmed, so it's
        // clear the preview is frozen at the last successful state rather than reflecting the current broken markup.
        private var stale = false

        fun setStale(value: Boolean) {
            if (stale == value) return
            stale = value
            repaint()
        }

        /** Maps a canvas point to the frame's design space (the coordinates the host hit-tests in), or null when
         * the point is outside the drawn frame (so hovering the checkerboard clears the highlight). */
        fun canvasToDesign(p: Point): Point2D.Double? {
            val ds = designSize() ?: return null
            if (displayScale <= 0) return null
            val size = displayedSize()
            val ox = max(0, (width - size.width) / 2)
            val oy = max(0, (height - size.height) / 2)
            val dx = (p.x - ox) / displayScale
            val dy = (p.y - oy) / displayScale
            if (dx < 0 || dy < 0 || dx > ds.width || dy > ds.height) return null
            return Point2D.Double(dx, dy)
        }

        private fun displayedSize(): Dimension {
            val img = image ?: return Dimension(0, 0)
            val factor = displayScale / renderedScale
            return Dimension(max(1, (img.width * factor).roundToInt()), max(1, (img.height * factor).roundToInt()))
        }

        /** The frame's design size in px (the image divided by the scale it was rendered at), or null if empty. */
        fun designSize(): Dimension? {
            val img = image ?: return null
            return Dimension(max(1, (img.width / renderedScale).roundToInt()),
                             max(1, (img.height / renderedScale).roundToInt()))
        }

        override fun getPreferredSize(): Dimension = displayedSize()

        enum class Background { CHECKERBOARD, DARK, LIGHT }

        private var background = Background.CHECKERBOARD

        /** Cycles the canvas backdrop: checkerboard (shows transparency) -> solid dark -> solid light. */
        fun cycleBackground() {
            background = Background.entries[(background.ordinal + 1) % Background.entries.size]
            repaint()
        }

        override fun paintComponent(g: Graphics) {
            val g2 = g as Graphics2D
            when (background) {
                Background.CHECKERBOARD -> paintCheckerboard(g2)
                Background.DARK -> { g2.color = SOLID_DARK; g2.fillRect(0, 0, width, height) }
                Background.LIGHT -> { g2.color = SOLID_LIGHT; g2.fillRect(0, 0, width, height) }
            }
            val img = image ?: return
            val size = displayedSize()
            val x = max(0, (width - size.width) / 2)
            val y = max(0, (height - size.height) / 2)
            g2.setRenderingHint(RenderingHints.KEY_INTERPOLATION, RenderingHints.VALUE_INTERPOLATION_BILINEAR)
            g2.drawImage(img, x, y, size.width, size.height, null)
            // Stale: the current markup didn't render (transiently invalid); dim the kept last-good frame.
            if (stale) {
                g2.color = STALE_OVERLAY
                g2.fillRect(x, y, size.width, size.height)
            }
            // The hover and selection frames are drawn by the FRAMEWORK (into the rendered image), not here.
        }

        private fun paintCheckerboard(g2: Graphics2D) {
            var y = 0
            var row = 0
            while (y < height) {
                var x = 0
                var col = 0
                while (x < width) {
                    g2.color = if ((row + col) and 1 == 0) CHECKER_DARK else CHECKER_LIGHT
                    g2.fillRect(x, y, CHECKER_TILE, CHECKER_TILE)
                    x += CHECKER_TILE; col++
                }
                y += CHECKER_TILE; row++
            }
        }

        override fun getPreferredScrollableViewportSize(): Dimension = preferredSize
        override fun getScrollableUnitIncrement(visibleRect: Rectangle, orientation: Int, direction: Int): Int = 16
        override fun getScrollableBlockIncrement(visibleRect: Rectangle, orientation: Int, direction: Int): Int = 128
        override fun getScrollableTracksViewportWidth(): Boolean =
            (parent as? JViewport)?.let { it.width > displayedSize().width } ?: false
        override fun getScrollableTracksViewportHeight(): Boolean =
            (parent as? JViewport)?.let { it.height > displayedSize().height } ?: false
    }

    companion object {
        private const val DEBOUNCE_MS = 250
        private const val HOVER_DEBOUNCE_MS = 60
        private const val ZOOM_STEP = 1.25
        private const val MIN_SCALE = 0.05
        private const val MAX_SCALE = 8.0
        private val ZOOM_PRESETS = listOf(0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0)
        private const val CHECKER_TILE = 12
        private val STALE_OVERLAY = java.awt.Color(0, 0, 0, 90)   // ~35% dim over the kept frame when the markup is broken
        private val CHECKER_DARK = JBColor(0xC8C8C8, 0x3C3F41)
        private val CHECKER_LIGHT = JBColor(0xE8E8E8, 0x4B4F52)
        private val SOLID_DARK = JBColor(0x2B2B2B, 0x2B2B2B)
        private val SOLID_LIGHT = JBColor(0xFFFFFF, 0xFFFFFF)

        private fun percent(scale: Double): String = "${(scale * 100).roundToInt()}%"
    }
}
