package com.adamantium.auml

import com.intellij.ide.actions.CreateFileFromTemplateAction
import com.intellij.ide.actions.CreateFileFromTemplateDialog
import com.intellij.openapi.project.DumbAware
import com.intellij.openapi.project.Project
import com.intellij.psi.PsiDirectory

/**
 * Adds "AUML File" to the project view's New/Add context menu. Picking a kind (Window, View, Theme,
 * StyleSet, ResourceDictionary) creates a `.auml` file from the matching bundled template under
 * resources fileTemplates/internal, registered via the internalFileTemplate extension in plugin.xml.
 *
 * The kinds are exactly the AUML root entity types that have a working concrete form in the engine -
 * UIApplication/Page are intentionally absent until they have a real AUML shape to template from.
 */
class CreateAumlFileAction : CreateFileFromTemplateAction(
    "AUML File",
    "Creates a new Adamantium UI markup file",
    AumlFileType.getIcon()
), DumbAware {

    override fun buildDialog(project: Project, directory: PsiDirectory, builder: CreateFileFromTemplateDialog.Builder) {
        builder
            .setTitle("New AUML File")
            .addKind("Window", AumlFileType.getIcon(), "Adamantium Window")
            .addKind("View", AumlFileType.getIcon(), "Adamantium View")
            .addKind("Theme", AumlFileType.getIcon(), "Adamantium Theme")
            .addKind("Style Set", AumlFileType.getIcon(), "Adamantium StyleSet")
            .addKind("Resource Dictionary", AumlFileType.getIcon(), "Adamantium ResourceDictionary")
    }

    override fun getActionName(directory: PsiDirectory, newName: String, templateName: String): String =
        "Create AUML File: $newName"
}
