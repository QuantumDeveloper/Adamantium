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
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.ComboBox
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
import java.awt.Graphics
import java.awt.Graphics2D
import java.awt.Rectangle
import java.awt.RenderingHints
import java.awt.image.BufferedImage
import java.beans.PropertyChangeListener
import java.io.File
import javax.imageio.ImageIO
import javax.swing.JButton
import javax.swing.JComponent
import javax.swing.JPanel
import javax.swing.JViewport
import javax.swing.Scrollable
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

    private var scale = 1.0
    private var updatingZoom = false

    private val canvas = ZoomableCanvas()
    private val zoomCombo = ComboBox(ZOOM_PRESETS.map { percent(it) }.toTypedArray()).apply {
        isEditable = true
        selectedItem = percent(1.0)
        addActionListener { if (!updatingZoom) (selectedItem as? String)?.let(::applyZoomText) }
    }
    private val errorLabel = JBLabel().apply {
        foreground = JBColor.RED
        border = JBUI.Borders.emptyLeft(12)
        isVisible = false
    }
    private val scrollPane = JBScrollPane(canvas).apply { isWheelScrollingEnabled = false }
    private val root = JPanel(BorderLayout()).apply {
        add(buildToolbar(), BorderLayout.NORTH)
        add(scrollPane, BorderLayout.CENTER)
    }

    init {
        document?.addDocumentListener(object : DocumentListener {
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
        scheduleRender()
    }

    private fun buildToolbar(): JComponent = JPanel(FlowLayout(FlowLayout.LEFT, 4, 2)).apply {
        add(JButton("−").apply { addActionListener { setScale(scale / ZOOM_STEP) } })
        add(zoomCombo)
        add(JButton("+").apply { addActionListener { setScale(scale * ZOOM_STEP) } })
        add(JButton("100%").apply { addActionListener { setScale(1.0) } })
        add(errorLabel)
    }

    private fun applyZoomText(text: String) {
        val value = text.trim().removeSuffix("%").trim().replace(',', '.').toDoubleOrNull() ?: return
        setScale(value / 100.0)
    }

    private fun setScale(newScale: Double) {
        scale = newScale.coerceIn(MIN_SCALE, MAX_SCALE)
        canvas.displayScale = scale // instant feedback: scales the current frame until the re-render lands
        updatingZoom = true
        zoomCombo.selectedItem = percent(scale)
        updatingZoom = false
        scheduleRender()
    }

    private fun scheduleRender() {
        if (document == null) return
        alarm.cancelAllRequests()
        alarm.addRequest({ renderNow() }, DEBOUNCE_MS)
    }

    /** On the EDT: snapshot text + scale, then render off-thread and apply the result back on the EDT. */
    private fun renderNow() {
        val doc = document ?: return
        val text = doc.text
        val renderScale = scale
        ApplicationManager.getApplication().executeOnPooledThread {
            val result = service.render(text, renderScale)
            val image = result.pngPath?.let { runCatching { ImageIO.read(File(it)) }.getOrNull() }
            ApplicationManager.getApplication().invokeLater({ applyResult(result, image, renderScale) }, ModalityState.any())
        }
    }

    private fun applyResult(result: AumlPreviewService.RenderResult, image: BufferedImage?, renderedScale: Double) {
        if (image != null) {
            canvas.setImage(image, renderedScale) // updating only on success keeps the last good frame on failure
            errorLabel.isVisible = false
        } else {
            errorLabel.text = result.error ?: "render failed"
            errorLabel.isVisible = true
        }
    }

    override fun getComponent(): JComponent = root
    override fun getPreferredFocusedComponent(): JComponent = canvas
    override fun getName(): String = "Preview"
    override fun getFile(): VirtualFile = file
    override fun setState(state: FileEditorState) {}
    override fun getState(level: FileEditorStateLevel): FileEditorState = FileEditorState.INSTANCE
    override fun isModified(): Boolean = false
    override fun isValid(): Boolean = true
    override fun getCurrentLocation(): FileEditorLocation? = null
    override fun addPropertyChangeListener(listener: PropertyChangeListener) {}
    override fun removePropertyChangeListener(listener: PropertyChangeListener) {}
    override fun dispose() {}

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
            image = img
            renderedScale = scale
            displayScale = scale
        }

        private fun displayedSize(): Dimension {
            val img = image ?: return Dimension(0, 0)
            val factor = displayScale / renderedScale
            return Dimension(max(1, (img.width * factor).roundToInt()), max(1, (img.height * factor).roundToInt()))
        }

        override fun getPreferredSize(): Dimension = displayedSize()

        override fun paintComponent(g: Graphics) {
            val g2 = g as Graphics2D
            paintCheckerboard(g2)
            val img = image ?: return
            val size = displayedSize()
            val x = max(0, (width - size.width) / 2)
            val y = max(0, (height - size.height) / 2)
            g2.setRenderingHint(RenderingHints.KEY_INTERPOLATION, RenderingHints.VALUE_INTERPOLATION_BILINEAR)
            g2.drawImage(img, x, y, size.width, size.height, null)
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
        private const val ZOOM_STEP = 1.25
        private const val MIN_SCALE = 0.05
        private const val MAX_SCALE = 8.0
        private val ZOOM_PRESETS = listOf(0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0)
        private const val CHECKER_TILE = 12
        private val CHECKER_DARK = JBColor(0xC8C8C8, 0x3C3F41)
        private val CHECKER_LIGHT = JBColor(0xE8E8E8, 0x4B4F52)

        private fun percent(scale: Double): String = "${(scale * 100).roundToInt()}%"
    }
}
