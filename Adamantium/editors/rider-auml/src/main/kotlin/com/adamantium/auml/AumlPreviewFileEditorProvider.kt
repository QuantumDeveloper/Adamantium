package com.adamantium.auml

import com.intellij.openapi.fileEditor.FileEditor
import com.intellij.openapi.fileEditor.FileEditorPolicy
import com.intellij.openapi.fileEditor.FileEditorProvider
import com.intellij.openapi.fileEditor.TextEditor
import com.intellij.openapi.fileEditor.impl.text.TextEditorProvider
import com.intellij.openapi.fileEditor.TextEditorWithPreview
import com.intellij.openapi.project.DumbAware
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile

/**
 * Opens `.auml` files in a WPF-style split editor: the XML text on the left, the live engine-rendered
 * preview on the right (via [AumlPreviewFileEditor]). Replaces the plain XML editor for these files.
 */
class AumlPreviewFileEditorProvider : FileEditorProvider, DumbAware {
    override fun accept(project: Project, file: VirtualFile): Boolean = file.fileType == AumlFileType

    override fun createEditor(project: Project, file: VirtualFile): FileEditor {
        val textEditor = TextEditorProvider.getInstance().createEditor(project, file) as TextEditor
        val preview = AumlPreviewFileEditor(project, file)
        return TextEditorWithPreview(textEditor, preview, "AUML", TextEditorWithPreview.Layout.SHOW_EDITOR_AND_PREVIEW)
    }

    override fun getEditorTypeId(): String = "auml-split-editor"

    // Replace the default text editor with our split (the split still contains a full text editor).
    override fun getPolicy(): FileEditorPolicy = FileEditorPolicy.HIDE_DEFAULT_EDITOR
}
