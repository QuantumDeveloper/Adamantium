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
import com.intellij.openapi.util.UserDataHolderBase
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.ui.JBColor
import com.intellij.ui.components.JBLabel
import com.intellij.util.Alarm
import com.intellij.util.ui.JBUI
import java.awt.BorderLayout
import java.awt.Graphics
import java.awt.Graphics2D
import java.awt.RenderingHints
import java.awt.image.BufferedImage
import java.beans.PropertyChangeListener
import java.io.File
import javax.imageio.ImageIO
import javax.swing.JComponent
import javax.swing.JPanel

/**
 * Read-only preview half of the AUML split editor: renders the current editor buffer through the designer
 * host and paints the resulting image, scaled to fit. Re-renders live on edit (debounced); on a failed
 * render it shows an error banner and keeps the last good frame.
 */
class AumlPreviewFileEditor(
    private val project: Project,
    private val file: VirtualFile,
) : UserDataHolderBase(), FileEditor {

    private val service = project.getService(AumlPreviewService::class.java)
    private val document: Document? = FileDocumentManager.getInstance().getDocument(file)
    private val alarm = Alarm(Alarm.ThreadToUse.SWING_THREAD, this)

    private val canvas = ImageCanvas()
    private val banner = JBLabel().apply {
        isVisible = false
        foreground = JBColor.RED
        border = JBUI.Borders.empty(4, 8)
    }
    private val root = JPanel(BorderLayout()).apply {
        add(banner, BorderLayout.NORTH)
        add(canvas, BorderLayout.CENTER)
    }

    init {
        document?.addDocumentListener(object : DocumentListener {
            override fun documentChanged(event: DocumentEvent) = scheduleRender()
        }, this)
        scheduleRender() // initial preview
    }

    private fun scheduleRender() {
        if (document == null) return
        alarm.cancelAllRequests()
        alarm.addRequest({ renderNow() }, DEBOUNCE_MS)
    }

    /** On the EDT: snapshot text + size, then render off-thread and apply the result back on the EDT. */
    private fun renderNow() {
        val doc = document ?: return
        val text = doc.text
        val width = (if (canvas.width > 0) canvas.width else DEFAULT_W).coerceAtLeast(MIN_SIZE)
        val height = (if (canvas.height > 0) canvas.height else DEFAULT_H).coerceAtLeast(MIN_SIZE)

        ApplicationManager.getApplication().executeOnPooledThread {
            val result = service.render(text, width, height)
            val image = result.pngPath?.let { runCatching { ImageIO.read(File(it)) }.getOrNull() }
            ApplicationManager.getApplication().invokeLater({ applyResult(result, image) }, ModalityState.any())
        }
    }

    private fun applyResult(result: AumlPreviewService.RenderResult, image: BufferedImage?) {
        if (image != null) {
            canvas.image = image // updating only on success keeps the last good frame on a later failure
            banner.isVisible = false
        } else {
            banner.text = result.error ?: "render failed"
            banner.isVisible = true
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

    /** Paints a [BufferedImage] centred and scaled to fit, preserving aspect ratio. */
    private class ImageCanvas : JComponent() {
        var image: BufferedImage? = null
            set(value) { field = value; repaint() }

        override fun paintComponent(g: Graphics) {
            super.paintComponent(g)
            val img = image ?: return
            val pw = width
            val ph = height
            if (pw <= 0 || ph <= 0) return
            val scale = minOf(pw.toDouble() / img.width, ph.toDouble() / img.height)
            val dw = (img.width * scale).toInt()
            val dh = (img.height * scale).toInt()
            val x = (pw - dw) / 2
            val y = (ph - dh) / 2
            (g as Graphics2D).setRenderingHint(
                RenderingHints.KEY_INTERPOLATION, RenderingHints.VALUE_INTERPOLATION_BILINEAR
            )
            g.drawImage(img, x, y, dw, dh, null)
        }
    }

    companion object {
        private const val DEBOUNCE_MS = 300
        private const val DEFAULT_W = 1280
        private const val DEFAULT_H = 720
        private const val MIN_SIZE = 16
    }
}
