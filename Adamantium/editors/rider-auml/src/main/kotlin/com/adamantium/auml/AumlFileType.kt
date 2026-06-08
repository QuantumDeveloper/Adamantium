package com.adamantium.auml

import com.intellij.lang.xml.XMLLanguage
import com.intellij.openapi.fileTypes.LanguageFileType
import com.intellij.openapi.util.IconLoader
import javax.swing.Icon

/**
 * The `.auml` file type, backed by the XML language so IntelliJ/Rider give full XML syntax
 * highlighting, structure, and folding for free. Registered in plugin.xml.
 */
object AumlFileType : LanguageFileType(XMLLanguage.INSTANCE) {
    private val icon: Icon = IconLoader.getIcon("/icons/auml.svg", AumlFileType::class.java)

    override fun getName(): String = "AUML"
    override fun getDescription(): String = "Adamantium UI markup"
    override fun getDefaultExtension(): String = "auml"
    override fun getIcon(): Icon = icon
}
