package com.adamantium.auml

import com.intellij.javaee.ExternalResourceManagerEx
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.fileEditor.FileEditorManagerListener
import com.intellij.openapi.vfs.VirtualFile

/**
 * Marks a .auml file's `clr-namespace:` xmlns URIs as "ignored" XML resources so the built-in XML
 * support treats them as known (no red "URI is not registered"). This is the same mechanism that
 * registers the static `http://adamantium/ui` namespaces (see [AumlResourceProvider]), applied
 * dynamically per file because `clr-namespace:` URIs vary from file to file and can't be enumerated
 * ahead of time. It resolves the namespace reference upstream, so no error highlight is produced —
 * more reliable than filtering the highlight after the fact. The AUML language server stays the real
 * validator: it flags a `clr-namespace:` that doesn't resolve to any type.
 */
class AumlClrNamespaceRegistrar : FileEditorManagerListener {
    override fun fileOpened(source: FileEditorManager, file: VirtualFile) {
        if (!file.name.endsWith(".auml", ignoreCase = true)) return

        // Run after the open completes; reading the document + touching settings is safest off the
        // critical path, and re-highlighting then picks up the newly-ignored URIs.
        ApplicationManager.getApplication().invokeLater {
            val text = FileDocumentManager.getInstance().getDocument(file)?.text ?: return@invokeLater
            val manager = ExternalResourceManagerEx.getInstanceEx()
            val toIgnore = CLR_NAMESPACE.findAll(text)
                .map { it.value }
                .filterNot { manager.isIgnoredResource(it) }
                .distinct()
                .toList()
            if (toIgnore.isEmpty()) return@invokeLater

            // addIgnoredResources(List<String>, Disposable); scoped to the project so it clears on close.
            ApplicationManager.getApplication().runWriteAction(Runnable {
                try {
                    manager.addIgnoredResources(toIgnore, source.project)
                } catch (_: Throwable) {
                }
            })
        }
    }

    private companion object {
        val CLR_NAMESPACE = Regex("""clr-namespace:[^"']+""")
    }
}
